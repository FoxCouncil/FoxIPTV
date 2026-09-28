// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback.Hls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Classes;

    public interface ISourceEvents
    {
        void OnProtected(string why);

        void OnSourceFailed(string why);

        void OnPlaylist(string track, HlsPlaylist playlist, TimeSpan took);

        void OnPiece(string track, MediaChunk chunk, TimeSpan took);

        void OnVariant(string description);
    }

    public sealed class HlsLoader
    {
        private const int StartPieces = 3;

        private static readonly TimeSpan PlaylistTimeout = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan FailureBudget = TimeSpan.FromSeconds(30);

        private readonly MediaRequest _request;

        private readonly ISourceEvents _events;

        private readonly CancellationToken _token;

        private readonly Dictionary<Uri, byte[]> _keys = new Dictionary<Uri, byte[]>();

        private readonly Dictionary<string, byte[]> _maps = new Dictionary<string, byte[]>();

        private List<HlsVariant> _variants = new List<HlsVariant>();

        private int _variantIndex = -1;

        private double _throughput;

        private int _slowPieces;

        private readonly Stopwatch _sinceSwitch = Stopwatch.StartNew();

        public HlsLoader(MediaRequest request, ISourceEvents events, CancellationToken token)
        {
            _request = request;
            _events = events;
            _token = token;
        }

        public ChunkQueue Main { get; } = new ChunkQueue();

        public ChunkQueue Audio { get; private set; }

        public bool IsLive { get; private set; } = true;

        public double TargetDuration { get; private set; }

        public string VariantLabel => _variantIndex >= 0 && _variantIndex < _variants.Count ? _variants[_variantIndex].ToString() : null;

        public void Start(HlsPlaylist entry)
        {
            var main = entry;
            Uri audioUri = null;

            if (entry.IsMaster)
            {
                if (entry.SessionKeys.Any(x => x.IsCopyProtection))
                {
                    _events.OnProtected($"session key {entry.SessionKeys.First(x => x.IsCopyProtection).Method} {entry.SessionKeys.First(x => x.IsCopyProtection).KeyFormat}");

                    return;
                }

                var video = entry.Variants.Where(x => x.HasVideo).ToList();

                _variants = (video.Count > 0 ? video : entry.Variants).OrderBy(x => x.Bandwidth).ToList();
                _variantIndex = _variants.Count - 1;

                var chosen = _variants[_variantIndex];

                _events.OnVariant(chosen.ToString());

                TvCore.LogInfo($"[Player] HLS {entry.Describe()}; starting with {chosen}");

                var group = entry.Renditions.Where(x => string.Equals(x.Type, "AUDIO", StringComparison.OrdinalIgnoreCase) && x.GroupId == chosen.AudioGroup && chosen.AudioGroup != null).ToList();
                var audio = group.FirstOrDefault(x => x.IsDefault) ?? group.FirstOrDefault(x => x.AutoSelect) ?? group.FirstOrDefault();

                if (audio?.Uri != null)
                {
                    audioUri = audio.Uri;
                    Audio = new ChunkQueue();

                    TvCore.LogInfo($"[Player] HLS audio comes from its own playlist: {audio}");
                }

                main = null;

                _ = Task.Run(() => RunTrack("video", Main, chosen.Uri, null, true));
            }
            else
            {
                IsLive = entry.IsLive;
                TargetDuration = entry.TargetDuration;

                TvCore.LogInfo($"[Player] HLS {entry.Describe()}");

                _ = Task.Run(() => RunTrack("video", Main, entry.Uri, main, true));
            }

            if (audioUri != null)
            {
                _ = Task.Run(() => RunTrack("audio", Audio, audioUri, null, false));
            }
        }

        private async Task RunTrack(string name, ChunkQueue queue, Uri playlistUri, HlsPlaylist first, bool isMain)
        {
            try
            {
                await Track(name, queue, playlistUri, first, isMain).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!_token.IsCancellationRequested)
                {
                    TvCore.LogError($"[Player] HLS {name} track stopped: {ex.GetType().Name}: {ex.Message}");

                    _events.OnSourceFailed(ex.Message);
                }
            }
            finally
            {
                queue.Complete();
            }
        }

        private async Task Track(string name, ChunkQueue queue, Uri playlistUri, HlsPlaylist playlist, bool isMain)
        {
            long? next = null;
            var period = 0;
            int? lastDiscontinuity = null;
            string lastMap = null;
            var changed = true;
            var loadedAt = Stopwatch.StartNew();
            var failingSince = (Stopwatch)null;
            var forceReload = playlist == null;
            var switched = false;

            while (!_token.IsCancellationRequested)
            {
                if (forceReload)
                {
                    var previous = playlist;

                    loadedAt.Restart();

                    playlist = await LoadPlaylist(name, playlistUri).ConfigureAwait(false);

                    changed = previous == null || previous.Segments.Count != playlist.Segments.Count || previous.MediaSequence != playlist.MediaSequence || previous.EndList != playlist.EndList;
                    forceReload = false;

                    if (isMain)
                    {
                        IsLive = playlist.IsLive;
                        TargetDuration = playlist.TargetDuration;
                    }
                }

                if (playlist.Segments.Count == 0)
                {
                    await Wait(Math.Max(1, playlist.TargetDuration / 2), loadedAt).ConfigureAwait(false);

                    forceReload = true;

                    continue;
                }

                next ??= StartSequence(playlist);

                var segment = playlist.Segments.FirstOrDefault(x => x.Sequence == next.Value);

                if (segment == null)
                {
                    var head = playlist.Segments[0];
                    var tail = playlist.Segments[playlist.Segments.Count - 1];

                    if (next.Value < head.Sequence || next.Value > tail.Sequence + 1 + playlist.Segments.Count)
                    {
                        var restart = StartSequence(playlist);

                        TvCore.LogError($"[Player] HLS {name}: wanted piece #{next} but the playlist holds #{head.Sequence}-#{tail.Sequence}; moving to #{restart}");

                        next = restart;
                        period++;
                        lastDiscontinuity = null;
                        lastMap = null;

                        continue;
                    }

                    if (!playlist.IsLive)
                    {
                        TvCore.LogInfo($"[Player] HLS {name}: reached the end of the playlist");

                        return;
                    }

                    await Wait(changed ? Math.Max(1, tail.Duration) : Math.Max(0.5, playlist.TargetDuration / 2), loadedAt).ConfigureAwait(false);

                    forceReload = true;

                    continue;
                }

                if (segment.Key != null && segment.Key.IsCopyProtection)
                {
                    _events.OnProtected($"key {segment.Key.Method} {segment.Key.KeyFormat}");

                    return;
                }

                var discontinuity = lastDiscontinuity.HasValue && segment.DiscontinuitySequence != lastDiscontinuity.Value;
                var mapChanged = lastMap != null && segment.Map?.Id != lastMap;

                if (discontinuity || mapChanged || switched)
                {
                    period++;
                }

                if (segment.Gap)
                {
                    next++;
                    lastDiscontinuity = segment.DiscontinuitySequence;

                    continue;
                }

                if (segment.Map != null && (lastMap != segment.Map.Id || discontinuity || switched))
                {
                    var init = await FetchMap(segment.Map).ConfigureAwait(false);

                    queue.Add(new MediaChunk { Data = init, IsInit = true, Period = period, Discontinuity = segment.DiscontinuitySequence, Url = segment.Map.Uri.ToString() }, _token);
                }

                lastMap = segment.Map?.Id;
                lastDiscontinuity = segment.DiscontinuitySequence;
                switched = false;

                var clock = Stopwatch.StartNew();

                byte[] data;

                try
                {
                    data = await FetchSegment(segment).ConfigureAwait(false);

                    failingSince = null;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException) || !_token.IsCancellationRequested)
                {
                    failingSince ??= Stopwatch.StartNew();

                    TvCore.LogError($"[Player] HLS {name}: piece #{segment.Sequence} failed ({ex.Message}){(playlist.IsLive ? ", skipping it" : string.Empty)}");

                    if (failingSince.Elapsed > FailureBudget)
                    {
                        throw new PlayerException($"No piece loaded for {failingSince.Elapsed.TotalSeconds:0}s: {ex.Message}");
                    }

                    if (playlist.IsLive)
                    {
                        next++;
                        forceReload = true;
                    }
                    else
                    {
                        await Task.Delay(1000, _token).ConfigureAwait(false);
                    }

                    continue;
                }

                var chunk = new MediaChunk
                {
                    Data = data,
                    Period = period,
                    Discontinuity = segment.DiscontinuitySequence,
                    Duration = segment.Duration,
                    Url = segment.Uri.ToString(),
                    Title = segment.Title,
                    StartsDiscontinuity = discontinuity
                };

                _events.OnPiece(name, chunk, clock.Elapsed);

                queue.Add(chunk, _token);

                next++;

                if (isMain && _variants.Count > 1)
                {
                    var target = Adapt(segment.Duration, data.Length, clock.Elapsed, queue.BufferedSeconds);

                    if (target != _variantIndex)
                    {
                        var from = _variants[_variantIndex];

                        _variantIndex = target;
                        _sinceSwitch.Restart();

                        playlistUri = _variants[_variantIndex].Uri;
                        forceReload = true;
                        switched = true;

                        TvCore.LogInfo($"[Player] HLS switching from {from} to {_variants[_variantIndex]} (throughput {_throughput * 8 / 1000:0}kbps)");

                        _events.OnVariant(_variants[_variantIndex].ToString());
                    }
                }

                if (playlist.IsLive && loadedAt.Elapsed.TotalSeconds >= Math.Max(1, playlist.TargetDuration) && segment == playlist.Segments[playlist.Segments.Count - 1])
                {
                    forceReload = true;
                }
            }
        }

        private int Adapt(double duration, int bytes, TimeSpan took, double buffered)
        {
            var seconds = Math.Max(0.001, took.TotalSeconds);
            var rate = bytes / seconds;

            _throughput = _throughput <= 0 ? rate : _throughput * 0.7 + rate * 0.3;

            if (duration > 0 && seconds > duration * 0.8)
            {
                _slowPieces++;
            }
            else
            {
                _slowPieces = 0;
            }

            if (_slowPieces >= 2 && _variantIndex > 0)
            {
                _slowPieces = 0;

                var fit = _variantIndex - 1;

                while (fit > 0 && _variants[fit].Bandwidth > _throughput * 8 * 0.8)
                {
                    fit--;
                }

                return fit;
            }

            if (_variantIndex < _variants.Count - 1 && _sinceSwitch.Elapsed.TotalSeconds > 30 && buffered >= 10 && _variants[_variantIndex + 1].Bandwidth < _throughput * 8 * 0.6)
            {
                return _variantIndex + 1;
            }

            return _variantIndex;
        }

        private static long StartSequence(HlsPlaylist playlist)
        {
            if (!playlist.IsLive)
            {
                return playlist.Segments[0].Sequence;
            }

            var index = Math.Max(0, playlist.Segments.Count - StartPieces);

            return playlist.Segments[index].Sequence;
        }

        private async Task Wait(double seconds, Stopwatch since)
        {
            var remaining = TimeSpan.FromSeconds(seconds) - since.Elapsed;

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, _token).ConfigureAwait(false);
            }
        }

        private async Task<HlsPlaylist> LoadPlaylist(string name, Uri uri)
        {
            var failing = Stopwatch.StartNew();
            var attempt = 0;

            while (true)
            {
                var clock = Stopwatch.StartNew();

                try
                {
                    var (data, finalUri) = await MediaHttp.GetBytes(uri, _request.Headers, PlaylistTimeout, _token).ConfigureAwait(false);
                    var text = Web.Decode(data);

                    if (!HlsPlaylist.LooksLikePlaylist(text))
                    {
                        throw new PlayerException("the playlist answer is not a playlist");
                    }

                    var playlist = HlsPlaylist.Parse(text, finalUri);

                    _events.OnPlaylist(name, playlist, clock.Elapsed);

                    return playlist;
                }
                catch (Exception ex) when (!_token.IsCancellationRequested && !(ex is PlayerException && failing.Elapsed > FailureBudget))
                {
                    attempt++;

                    if (failing.Elapsed > FailureBudget || ex is HttpRequestException http && (http.StatusCode == HttpStatusCode.Forbidden || http.StatusCode == HttpStatusCode.Unauthorized) && attempt >= 3)
                    {
                        throw new PlayerException($"Playlist unreachable for {failing.Elapsed.TotalSeconds:0}s: {ex.Message}");
                    }

                    TvCore.LogError($"[Player] HLS {name}: playlist load failed (try {attempt}): {ex.Message}");

                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(4, attempt)), _token).ConfigureAwait(false);
                }
            }
        }

        private async Task<byte[]> FetchMap(HlsMap map)
        {
            if (_maps.TryGetValue(map.Id, out var cached))
            {
                return cached;
            }

            var (data, _) = await MediaHttp.GetBytes(map.Uri, _request.Headers, TimeSpan.FromSeconds(15), _token, map.Length.HasValue ? map.Offset ?? 0 : (long?)null, map.Length).ConfigureAwait(false);

            if (map.Key != null && map.Key.IsAes128)
            {
                data = await Decrypt(data, map.Key, 0).ConfigureAwait(false);
            }

            _maps[map.Id] = data;

            return data;
        }

        private async Task<byte[]> FetchSegment(HlsSegment segment)
        {
            var timeout = TimeSpan.FromSeconds(Math.Max(10, segment.Duration * 4));

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var (data, _) = await MediaHttp.GetBytes(segment.Uri, _request.Headers, timeout, _token, segment.Offset, segment.Length).ConfigureAwait(false);

                    if (segment.Key != null && segment.Key.IsAes128)
                    {
                        data = await Decrypt(data, segment.Key, segment.Sequence).ConfigureAwait(false);
                    }

                    return data;
                }
                catch (HttpRequestException ex) when (attempt < 2 && ex.StatusCode != HttpStatusCode.NotFound && ex.StatusCode != HttpStatusCode.Gone && !_token.IsCancellationRequested)
                {
                    await Task.Delay(300, _token).ConfigureAwait(false);
                }
                catch (TimeoutException) when (attempt < 2 && !_token.IsCancellationRequested)
                {
                }
            }
        }

        private async Task<byte[]> Decrypt(byte[] data, HlsKey key, long sequence)
        {
            if (key.Uri == null)
            {
                throw new PlayerException("AES-128 key has no address");
            }

            if (!_keys.TryGetValue(key.Uri, out var secret))
            {
                var (bytes, _) = await MediaHttp.GetBytes(key.Uri, _request.Headers, TimeSpan.FromSeconds(10), _token).ConfigureAwait(false);

                if (bytes.Length != 16)
                {
                    throw new PlayerException($"AES-128 key is {bytes.Length} bytes, not 16");
                }

                secret = bytes;
                _keys[key.Uri] = secret;
            }

            var iv = key.Iv;

            if (iv == null)
            {
                iv = new byte[16];

                for (var i = 0; i < 8; i++)
                {
                    iv[15 - i] = (byte)(sequence >> (8 * i));
                }
            }

            using (var aes = Aes.Create())
            {
                aes.Key = secret;

                try
                {
                    return aes.DecryptCbc(data, iv, PaddingMode.PKCS7);
                }
                catch (CryptographicException)
                {
                    return aes.DecryptCbc(data, iv, PaddingMode.None);
                }
            }
        }

        public static string Sniff(byte[] data)
        {
            if (data == null || data.Length < 8)
            {
                return null;
            }

            var offset = 0;

            if (data[0] == 'I' && data[1] == 'D' && data[2] == '3' && data.Length > 10)
            {
                offset = 10 + ((data[6] & 0x7F) << 21 | (data[7] & 0x7F) << 14 | (data[8] & 0x7F) << 7 | (data[9] & 0x7F));
            }

            if (data[0] == 0x47 && (data.Length < 189 || data[188] == 0x47))
            {
                return "mpegts";
            }

            if (offset == 0 && data.Length > 8)
            {
                var box = Encoding.ASCII.GetString(data, 4, 4);

                if (box == "ftyp" || box == "styp" || box == "moof" || box == "moov" || box == "sidx" || box == "emsg" || box == "prft")
                {
                    return "mov";
                }
            }

            if (offset + 2 < data.Length)
            {
                if (data[offset] == 0xFF && (data[offset + 1] & 0xF6) == 0xF0)
                {
                    return "aac";
                }

                if (data[offset] == 0x0B && data[offset + 1] == 0x77)
                {
                    return "ac3";
                }

                if (data[offset] == 0xFF && (data[offset + 1] & 0xE0) == 0xE0)
                {
                    return "mp3";
                }
            }

            return null;
        }
    }
}
