// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Classes;
    using FFmpeg.AutoGen;
    using Hls;

    public sealed class PlaybackSession : ISourceEvents
    {
        private const double StartAudioSeconds = 0.25;

        private const double AudioAheadSeconds = 0.4;

        private const double RebufferSeconds = 3.0;

        private static int _ids;

        private readonly Player _owner;

        private readonly MediaRequest _request;

        private readonly AudioOutput _audio;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly CancellationToken _token;

        private readonly Timeline _timeline = new Timeline();

        private readonly PacketQueue _videoPackets = new PacketQueue(2.0, 600);

        private readonly PacketQueue _audioPackets = new PacketQueue(2.0, 1500);

        private readonly WallClock _wall = new WallClock();

        private readonly List<Thread> _threads = new List<Thread>();

        private readonly List<Tuple<double, MediaChunk>> _pieceMarks = new List<Tuple<double, MediaChunk>>();

        private readonly List<Tuple<double, string>> _captionMarks = new List<Tuple<double, string>>();

        private readonly Stopwatch _age = Stopwatch.StartNew();

        private readonly object _infoLock = new object();

        private StreamInfo _info = new StreamInfo();

        private HlsLoader _hls;

        private OpenedSource _opened;

        private Timer _monitor;

        private int _demuxersRunning;

        private int _demuxersStarted;

        private int _demuxersOpened;

        private volatile bool _expectVideo;

        private volatile bool _expectAudio;

        private volatile bool _videoReady;

        private volatile bool _audioReady;

        private volatile bool _started;

        private volatile bool _audioClock;

        private volatile bool _buffering;

        private volatile bool _videoDone;

        private volatile bool _audioDone;

        private volatile bool _finished;

        private volatile bool _posterTaken;

        private double _readySince = double.NaN;

        private long _bytes;

        private int _pieces;

        private long _lastPieceAt = -1;

        private long _lastPlaylistAt = -1;

        private long _framesDecoded;

        private long _framesShown;

        private long _underruns;

        private string _variant;

        private string _lastCaption;

        private int _audioSession;

        private int _monitoring;

        public PlaybackSession(Player owner, MediaRequest request, AudioOutput audio)
        {
            _owner = owner;
            _request = request;
            _audio = audio;
            _token = _cts.Token;

            Id = Interlocked.Increment(ref _ids);
        }

        public int Id { get; }

        public FrameQueue Frames { get; } = new FrameQueue(8);

        public bool IsStopped => _token.IsCancellationRequested;

        public double Clock
        {
            get
            {
                if (!_started)
                {
                    return double.NaN;
                }

                return _audioClock ? _audio.Clock : _wall.Now;
            }
        }

        public StreamInfo Info
        {
            get
            {
                lock (_infoLock)
                {
                    return _info.Clone();
                }
            }
        }

        public PlayerStats Stats
        {
            get
            {
                var now = _age.ElapsedMilliseconds;

                return new PlayerStats
                {
                    BufferedSeconds = BufferedSeconds(),
                    BufferTargetSeconds = RefillTarget,
                    BytesLoaded = Interlocked.Read(ref _bytes),
                    PiecesLoaded = _pieces,
                    LastPieceAgo = Interlocked.Read(ref _lastPieceAt) < 0 ? -1 : (now - Interlocked.Read(ref _lastPieceAt)) / 1000.0,
                    LastPlaylistAgo = Interlocked.Read(ref _lastPlaylistAt) < 0 ? -1 : (now - Interlocked.Read(ref _lastPlaylistAt)) / 1000.0,
                    FramesDecoded = Interlocked.Read(ref _framesDecoded),
                    FramesShown = Interlocked.Read(ref _framesShown),
                    FramesDropped = Frames.Dropped,
                    AudioUnderruns = Interlocked.Read(ref _underruns),
                    Clock = Clock,
                    Variant = _variant
                };
            }
        }

        public void Start()
        {
            _audioSession = _audio.Claim(Id);

            _monitor = new Timer(state => Monitor(), null, 100, 100);

            Task.Run(Open);
        }

        public void Stop()
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            _monitor?.Dispose();
        }

        public void Close()
        {
            Stop();

            Thread[] threads;

            lock (_threads)
            {
                threads = _threads.ToArray();
            }

            foreach (var thread in threads)
            {
                if (!thread.Join(TimeSpan.FromSeconds(5)))
                {
                    TvCore.LogError($"[Player] Session {Id}: thread {thread.Name} did not stop within 5s");
                }
            }

            _videoPackets.Clear();
            _audioPackets.Clear();
            Frames.Clear();

            try
            {
                _opened?.Dispose();
            }
            catch (Exception)
            {
            }

            TvCore.LogInfo($"[Player] Session {Id} closed after {_age.Elapsed.TotalSeconds:0.0}s: {Stats}");
        }

        private async Task Open()
        {
            try
            {
                _owner.Report(this, PlayerState.Opening, null);

                PlaybackTrace.Mark("open", _request.Uri.Host);

                _opened = await OpenedSource.Open(_request, _token).ConfigureAwait(false);

                if (_token.IsCancellationRequested)
                {
                    return;
                }

                if (_opened.Playlist != null)
                {
                    PlaybackTrace.Mark("playlist", _opened.Playlist.IsMaster ? $"{_opened.Playlist.Variants.Count} qualities" : $"{_opened.Playlist.Segments.Count} pieces");

                    Interlocked.Exchange(ref _lastPlaylistAt, _age.ElapsedMilliseconds);

                    _hls = new HlsLoader(_request, this, _token);
                    _hls.Start(_opened.Playlist);

                    var separateAudio = _hls.Audio != null;

                    StartThread("demux", () => DemuxChunks(_hls.Main, true, !separateAudio, true));

                    if (separateAudio)
                    {
                        StartThread("demux audio", () => DemuxChunks(_hls.Audio, false, true, false));
                    }
                }
                else if (_opened.Reader != null)
                {
                    PlaybackTrace.Mark("plain stream");

                    _opened.Reader.OnBytes = count => Interlocked.Add(ref _bytes, count);

                    StartThread("demux", DemuxProgressive);
                }
                else
                {
                    StartThread("demux", DemuxUrl);
                }

                StartThread("video", VideoLoop);
                StartThread("audio", AudioLoop);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Fail($"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private void StartThread(string name, Action body)
        {
            if (_token.IsCancellationRequested)
            {
                return;
            }

            if (name.StartsWith("demux", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _demuxersStarted);
            }

            var thread = new Thread(() =>
            {
                try
                {
                    body();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    if (!_token.IsCancellationRequested)
                    {
                        TvCore.LogError($"[Player] Session {Id} {name} thread failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

                        Fail(ex.Message);
                    }
                }
            })
            {
                IsBackground = true,
                Name = $"Player {Id} {name}"
            };

            lock (_threads)
            {
                _threads.Add(thread);
            }

            thread.Start();
        }

        private void Fail(string why)
        {
            if (_token.IsCancellationRequested)
            {
                return;
            }

            TvCore.LogError($"[Player] Session {Id} failed: {why}");

            PlaybackTrace.Mark("failed", why);

            _owner.Report(this, PlayerState.Failed, why);

            Stop();
        }

        public void OnProtected(string why)
        {
            if (_token.IsCancellationRequested)
            {
                return;
            }

            TvCore.LogInfo($"[Player] Session {Id}: copy-protected ({why})");

            PlaybackTrace.Mark("copy-protected", why);

            _owner.Report(this, PlayerState.Protected, why);

            Stop();
        }

        public void OnSourceFailed(string why)
        {
            Fail(why);
        }

        public void OnPlaylist(string track, HlsPlaylist playlist, TimeSpan took)
        {
            Interlocked.Exchange(ref _lastPlaylistAt, _age.ElapsedMilliseconds);

            if (took.TotalSeconds > 3)
            {
                TvCore.LogError($"[Player] HLS {track} playlist took {took.TotalMilliseconds:0}ms");
            }
        }

        public void OnPiece(string track, MediaChunk chunk, TimeSpan took)
        {
            Interlocked.Add(ref _bytes, chunk.Data.Length);
            Interlocked.Increment(ref _pieces);
            Interlocked.Exchange(ref _lastPieceAt, _age.ElapsedMilliseconds);

            PlaybackTrace.MarkOnce($"first {track} piece", $"{chunk.Data.Length / 1024}KB in {took.TotalMilliseconds:0}ms");

            if (chunk.Duration > 0 && took.TotalSeconds > chunk.Duration)
            {
                TvCore.LogError($"[Player] HLS {track} piece took {took.TotalSeconds:0.0}s for {chunk.Duration:0.0}s of media: {chunk.Url}");
            }
        }

        public void OnVariant(string description)
        {
            _variant = description;

            PlaybackTrace.Quality(description);
        }

        private double RefillTarget => _hls != null ? RebufferSeconds : 1.5;

        private double BufferedSeconds()
        {
            var queued = Math.Max(_videoPackets.Seconds, _audioPackets.Seconds);

            if (_hls != null)
            {
                queued += _hls.Audio == null ? _hls.Main.BufferedSeconds : Math.Min(_hls.Main.BufferedSeconds, _hls.Audio.BufferedSeconds);
            }

            return queued + (_audioClock ? _audio.QueuedSeconds : 0);
        }

        private unsafe void DemuxChunks(ChunkQueue queue, bool main, bool feedsAudio, bool feedsVideo)
        {
            Interlocked.Increment(ref _demuxersRunning);

            var state = new DemuxState();
            var packet = ffmpeg.av_packet_alloc();
            var skip = new byte[65536];

            try
            {
                while (!_token.IsCancellationRequested)
                {
                    var first = queue.Peek(_token);

                    if (first == null)
                    {
                        break;
                    }

                    var reader = new ChunkReader(queue, first.Period, _token);
                    var hint = first.IsInit ? "mov" : HlsLoader.Sniff(first.Data);

                    Demuxer demuxer;

                    try
                    {
                        demuxer = Demuxer.OpenCustom(reader.Read, null, hint, false, _token);
                    }
                    catch (Exception ex) when (!_token.IsCancellationRequested)
                    {
                        TvCore.LogError($"[Player] Session {Id}: period {first.Period} could not be opened ({ex.Message}), skipping it");

                        fixed (byte* buffer = skip)
                        {
                            while (reader.Read(buffer, skip.Length) > 0)
                            {
                            }
                        }

                        continue;
                    }

                    using (demuxer)
                    {
                        TvCore.LogInfo($"[Player] Session {Id}: period {first.Period} (run {first.Discontinuity}) {demuxer.Describe()}");

                        Pump(demuxer, main ? reader : null, first.Discontinuity, feedsVideo, feedsAudio, packet, state);
                    }

                    if (state.VideoParameters != null)
                    {
                        _videoPackets.Add(new PacketItem { Kind = PacketKind.Drain }, _token);
                    }

                    if (state.AudioParameters != null)
                    {
                        _audioPackets.Add(new PacketItem { Kind = PacketKind.Drain }, _token);
                    }
                }
            }
            finally
            {
                ffmpeg.av_packet_free(&packet);

                FinishDemux(state, feedsVideo, feedsAudio);
            }
        }

        private unsafe void DemuxProgressive()
        {
            Interlocked.Increment(ref _demuxersRunning);

            var state = new DemuxState();
            var packet = ffmpeg.av_packet_alloc();

            try
            {
                var reader = _opened.Reader;

                using (var demuxer = Demuxer.OpenCustom(reader.Read, reader.CanSeek ? reader.Seek : null, null, true, _token))
                {
                    TvCore.LogInfo($"[Player] Session {Id}: {demuxer.Describe()}");

                    Pump(demuxer, null, 0, true, true, packet, state);
                }
            }
            finally
            {
                ffmpeg.av_packet_free(&packet);

                FinishDemux(state, true, true);
            }
        }

        private unsafe void DemuxUrl()
        {
            Interlocked.Increment(ref _demuxersRunning);

            var state = new DemuxState();
            var packet = ffmpeg.av_packet_alloc();

            try
            {
                using (var demuxer = Demuxer.OpenUrl(_opened.Url, _request.Headers, _token))
                {
                    TvCore.LogInfo($"[Player] Session {Id}: {demuxer.Describe()}");

                    Pump(demuxer, null, 0, true, true, packet, state);
                }
            }
            finally
            {
                ffmpeg.av_packet_free(&packet);

                FinishDemux(state, true, true);
            }
        }

        private void FinishDemux(DemuxState state, bool feedsVideo, bool feedsAudio)
        {
            state.Free();

            if (_token.IsCancellationRequested)
            {
                return;
            }

            if (feedsVideo)
            {
                _videoPackets.Add(new PacketItem { Kind = PacketKind.End }, _token);
            }

            if (feedsAudio)
            {
                _audioPackets.Add(new PacketItem { Kind = PacketKind.End }, _token);
            }

            if (Interlocked.Decrement(ref _demuxersRunning) == 0)
            {
                TvCore.LogInfo($"[Player] Session {Id}: the source has ended");
            }
        }

        private sealed unsafe class DemuxState
        {
            public AVCodecParameters* VideoParameters;

            public AVCodecParameters* AudioParameters;

            public int Key = int.MinValue;

            public int PreviousKey = int.MinValue;

            public double LastVideo = double.NaN;

            public double LastAudio = double.NaN;

            public int Errors;

            public void Free()
            {
                var video = VideoParameters;
                var audio = AudioParameters;

                if (video != null)
                {
                    ffmpeg.avcodec_parameters_free(&video);
                }

                if (audio != null)
                {
                    ffmpeg.avcodec_parameters_free(&audio);
                }

                VideoParameters = null;
                AudioParameters = null;
            }
        }

        private static unsafe bool SameCodec(AVCodecParameters* a, AVCodecParameters* b)
        {
            if (a == null || b == null || a->codec_id != b->codec_id || a->extradata_size != b->extradata_size)
            {
                return false;
            }

            for (var i = 0; i < a->extradata_size; i++)
            {
                if (a->extradata[i] != b->extradata[i])
                {
                    return false;
                }
            }

            return true;
        }

        private unsafe void Reconfigure(PacketQueue queue, AVStream* stream, ref AVCodecParameters* last)
        {
            if (SameCodec(last, stream->codecpar))
            {
                return;
            }

            var copy = ffmpeg.avcodec_parameters_alloc();

            ffmpeg.avcodec_parameters_copy(copy, stream->codecpar);

            if (last == null)
            {
                last = ffmpeg.avcodec_parameters_alloc();
            }

            ffmpeg.avcodec_parameters_copy(last, stream->codecpar);

            queue.Add(new PacketItem { Kind = PacketKind.Reconfigure, Parameters = copy }, _token);
        }

        private unsafe void Pump(Demuxer demuxer, ChunkReader reader, int key, bool feedsVideo, bool feedsAudio, AVPacket* packet, DemuxState state)
        {
            var videoIndex = feedsVideo ? demuxer.VideoIndex : -1;
            var audioIndex = feedsAudio ? demuxer.AudioIndex : -1;

            if (videoIndex >= 0)
            {
                _expectVideo = true;

                Reconfigure(_videoPackets, demuxer.Stream(videoIndex), ref state.VideoParameters);
            }

            if (audioIndex >= 0)
            {
                _expectAudio = true;

                Reconfigure(_audioPackets, demuxer.Stream(audioIndex), ref state.AudioParameters);
            }

            if (state.Key == int.MinValue || key != 0 || reader != null)
            {
                state.PreviousKey = state.Key == int.MinValue ? key : state.Key;
                state.Key = key;
            }

            Interlocked.Increment(ref _demuxersOpened);

            var firstPacket = true;

            while (!_token.IsCancellationRequested)
            {
                var result = demuxer.Read(packet);

                if (result == ffmpeg.AVERROR_EOF || result == ffmpeg.AVERROR_EXIT)
                {
                    break;
                }

                if (result == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                {
                    Thread.Sleep(5);

                    continue;
                }

                if (result < 0)
                {
                    if (++state.Errors % 20 == 1)
                    {
                        TvCore.LogError($"[Player] Session {Id}: read error {FFmpegNative.Error(result)} ({state.Errors} so far)");
                    }

                    if (state.Errors > 200)
                    {
                        break;
                    }

                    continue;
                }

                var index = packet->stream_index;
                var isVideo = index == videoIndex;
                var isAudio = index == audioIndex;

                if (!isVideo && !isAudio)
                {
                    ffmpeg.av_packet_unref(packet);

                    continue;
                }

                if (ffmpeg.av_packet_get_side_data(packet, AVPacketSideDataType.AV_PKT_DATA_ENCRYPTION_INFO, null) != null)
                {
                    ffmpeg.av_packet_unref(packet);

                    OnProtected("encrypted samples");

                    return;
                }

                var stream = demuxer.Stream(index);
                var timeBase = ffmpeg.av_q2d(stream->time_base);
                var stamp = packet->pts != ffmpeg.AV_NOPTS_VALUE ? packet->pts : packet->dts;
                var duration = packet->duration > 0 ? packet->duration * timeBase : Estimate(stream);
                var mapped = double.NaN;

                if (stamp != ffmpeg.AV_NOPTS_VALUE)
                {
                    var seconds = stamp * timeBase;
                    var packetKey = state.Key;
                    var last = isVideo ? state.LastVideo : state.LastAudio;

                    mapped = seconds + _timeline.Offset(packetKey, seconds);

                    if (_timeline.IsJump(mapped, last))
                    {
                        var viaPrevious = seconds + _timeline.Offset(state.PreviousKey, seconds);

                        if (state.PreviousKey != state.Key && !_timeline.IsJump(viaPrevious, last))
                        {
                            packetKey = state.PreviousKey;
                            mapped = viaPrevious;
                        }
                        else
                        {
                            state.PreviousKey = state.Key;
                            state.Key = _timeline.NewRun();
                            packetKey = state.Key;

                            mapped = seconds + _timeline.Offset(packetKey, seconds);

                            TvCore.LogInfo($"[Player] Session {Id}: {(isVideo ? "video" : "audio")} time jumped from {last:0.000}s to source {seconds:0.000}s, joined at {mapped:0.000}s");
                        }
                    }

                    var offset = _timeline.Offset(packetKey, seconds);

                    Rewrite(packet, timeBase, offset);

                    _timeline.NoteEnd(mapped + duration);

                    if (isVideo)
                    {
                        state.LastVideo = mapped;
                    }
                    else
                    {
                        state.LastAudio = mapped;
                    }
                }
                else
                {
                    packet->duration = (long)Math.Round(duration * ffmpeg.AV_TIME_BASE);
                    packet->time_base = new AVRational { num = 1, den = ffmpeg.AV_TIME_BASE };
                }

                if (firstPacket)
                {
                    firstPacket = false;

                    PlaybackTrace.MarkOnce("first packet", demuxer.FormatName);
                }

                if (reader != null && !double.IsNaN(mapped))
                {
                    var started = reader.TakeStarted();

                    if (started != null)
                    {
                        lock (_pieceMarks)
                        {
                            foreach (var chunk in started.Where(x => !x.IsInit))
                            {
                                _pieceMarks.Add(Tuple.Create(mapped, chunk));
                            }
                        }
                    }
                }

                var copy = ffmpeg.av_packet_alloc();

                ffmpeg.av_packet_move_ref(copy, packet);

                (isVideo ? _videoPackets : _audioPackets).Add(new PacketItem { Kind = PacketKind.Packet, Packet = copy, Duration = duration }, _token);
            }
        }

        private static unsafe void Rewrite(AVPacket* packet, double timeBase, double offset)
        {
            if (packet->pts != ffmpeg.AV_NOPTS_VALUE)
            {
                packet->pts = (long)Math.Round((packet->pts * timeBase + offset) * ffmpeg.AV_TIME_BASE);
            }

            if (packet->dts != ffmpeg.AV_NOPTS_VALUE)
            {
                packet->dts = (long)Math.Round((packet->dts * timeBase + offset) * ffmpeg.AV_TIME_BASE);
            }

            packet->duration = (long)Math.Round(packet->duration * timeBase * ffmpeg.AV_TIME_BASE);
            packet->time_base = new AVRational { num = 1, den = ffmpeg.AV_TIME_BASE };
        }

        private static unsafe double Estimate(AVStream* stream)
        {
            var parameters = stream->codecpar;

            if (parameters->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                var samples = parameters->frame_size > 0 ? parameters->frame_size : 1024;

                return parameters->sample_rate > 0 ? samples / (double)parameters->sample_rate : 0.021;
            }

            var rate = stream->avg_frame_rate.num > 0 && stream->avg_frame_rate.den > 0 ? stream->avg_frame_rate : stream->r_frame_rate;

            return rate.num > 0 && rate.den > 0 ? rate.den / (double)rate.num : 1 / 30.0;
        }

        private unsafe void VideoLoop()
        {
            VideoDecoder decoder = null;
            AVCodecParameters* parameters = null;
            var frame = ffmpeg.av_frame_alloc();
            var captions = CaptionDecoder.Open();
            var errors = 0;
            var state = new VideoState();

            try
            {
                while (!_token.IsCancellationRequested)
                {
                    var item = _videoPackets.Take(_token);

                    if (item == null)
                    {
                        break;
                    }

                    try
                    {
                        switch (item.Kind)
                        {
                            case PacketKind.Reconfigure:
                            {
                                Drain(decoder, frame, captions, state);

                                decoder?.Dispose();
                                decoder = null;

                                if (parameters != null)
                                {
                                    ffmpeg.avcodec_parameters_free(&parameters);
                                }

                                parameters = item.Parameters;
                                item.Parameters = null;

                                try
                                {
                                    decoder = OpenVideo(parameters, true);
                                }
                                catch (Exception ex)
                                {
                                    TvCore.LogError($"[Player] Session {Id}: no pictures, {ex.Message}");
                                }

                                errors = 0;
                            }
                            break;

                            case PacketKind.Drain:
                            {
                                Drain(decoder, frame, captions, state);

                                decoder?.Flush();
                            }
                            break;

                            case PacketKind.End:
                            {
                                Drain(decoder, frame, captions, state);

                                return;
                            }

                            case PacketKind.Packet:
                            {
                                if (decoder == null)
                                {
                                    break;
                                }

                                var sent = decoder.Send(item.Packet);

                                if (sent == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                                {
                                    Receive(decoder, frame, captions, state);

                                    sent = decoder.Send(item.Packet);
                                }

                                if (sent < 0 && sent != ffmpeg.AVERROR_EOF)
                                {
                                    errors++;

                                    if (decoder.UsesHardware && errors >= 10)
                                    {
                                        TvCore.LogError($"[Player] Session {Id}: {decoder.HardwareName} keeps failing ({FFmpegNative.Error(sent)}), decoding on the CPU from here");

                                        decoder.Dispose();
                                        decoder = OpenVideo(parameters, false);
                                        errors = 0;
                                    }
                                }
                                else
                                {
                                    errors = 0;
                                }

                                Receive(decoder, frame, captions, state);
                            }
                            break;
                        }
                    }
                    finally
                    {
                        item.Free();
                    }
                }
            }
            finally
            {
                _videoDone = true;

                Frames.Complete();

                decoder?.Dispose();
                captions?.Dispose();

                if (parameters != null)
                {
                    ffmpeg.avcodec_parameters_free(&parameters);
                }

                ffmpeg.av_frame_free(&frame);
            }
        }

        private sealed class VideoState
        {
            public double NextTime = double.NaN;

            public long Number;

            public bool LoggedFormat;
        }

        private unsafe VideoDecoder OpenVideo(AVCodecParameters* parameters, bool allowHardware)
        {
            var hardware = allowHardware ? _owner.Hardware : null;

            try
            {
                var decoder = VideoDecoder.Open(parameters, hardware);

                TvCore.LogInfo($"[Player] Session {Id}: video decoder {decoder.CodecName} on {(decoder.UsesHardware ? decoder.HardwareName : "the CPU")}");

                return decoder;
            }
            catch (Exception ex) when (hardware != null)
            {
                TvCore.LogError($"[Player] Session {Id}: {hardware.Name} decoder failed to open ({ex.Message}), using the CPU");

                return VideoDecoder.Open(parameters, null);
            }
        }

        private unsafe void Drain(VideoDecoder decoder, AVFrame* frame, CaptionDecoder captions, VideoState state)
        {
            if (decoder == null)
            {
                return;
            }

            decoder.Send(null);

            Receive(decoder, frame, captions, state);
        }

        private unsafe void Receive(VideoDecoder decoder, AVFrame* frame, CaptionDecoder captions, VideoState state)
        {
            if (decoder == null)
            {
                return;
            }

            while (!_token.IsCancellationRequested && decoder.Receive(frame) >= 0)
            {
                try
                {
                    OnVideoFrame(decoder, frame, captions, state);
                }
                finally
                {
                    ffmpeg.av_frame_unref(frame);
                }
            }
        }

        private unsafe void OnVideoFrame(VideoDecoder decoder, AVFrame* frame, CaptionDecoder captions, VideoState state)
        {
            var stamp = frame->best_effort_timestamp;
            var rate = decoder.Codec->framerate;
            var duration = frame->duration > 0 ? frame->duration / (double)ffmpeg.AV_TIME_BASE : rate.num > 0 && rate.den > 0 ? rate.den / (double)rate.num : 1 / 30.0;
            var time = stamp != ffmpeg.AV_NOPTS_VALUE ? stamp / (double)ffmpeg.AV_TIME_BASE : double.IsNaN(state.NextTime) ? 0 : state.NextTime;

            state.NextTime = time + duration;

            var caption = ffmpeg.av_frame_get_side_data(frame, AVFrameSideDataType.AV_FRAME_DATA_A53_CC);

            if (caption != null && captions != null && caption->size > 0)
            {
                var text = captions.Decode(caption->data, (int)caption->size, stamp);

                if (text != null)
                {
                    lock (_captionMarks)
                    {
                        _captionMarks.Add(Tuple.Create(time, text));
                    }
                }

                SetInfo(x => x.Captions = true);
            }

            var hardware = frame->hw_frames_ctx != null;

            if (!state.LoggedFormat)
            {
                state.LoggedFormat = true;

                TvCore.LogInfo($"[Player] Session {Id}: first picture {frame->width}x{frame->height} {ffmpeg.av_get_pix_fmt_name((AVPixelFormat)frame->format)} at {time:0.000}s, {(hardware ? "on the GPU" : "in memory")}, colour {frame->colorspace} {frame->color_range} {frame->color_trc}");
            }

            SetInfo(x =>
            {
                x.VideoCodec = decoder.CodecName;
                x.Width = frame->width;
                x.Height = frame->height;
                x.FrameRate = rate.num > 0 && rate.den > 0 ? rate.num / (double)rate.den : duration > 0 ? 1 / duration : 0;
                x.Interlaced = (frame->flags & ffmpeg.AV_FRAME_FLAG_INTERLACED) != 0;
                x.HardwareDecoding = hardware;
            });

            AVFrame* keep;

            if (hardware && _owner.WantsCpuFrames)
            {
                keep = ffmpeg.av_frame_alloc();

                if (ffmpeg.av_hwframe_transfer_data(keep, frame, 0) < 0)
                {
                    ffmpeg.av_frame_free(&keep);

                    return;
                }

                ffmpeg.av_frame_copy_props(keep, frame);

                hardware = false;
            }
            else
            {
                keep = ffmpeg.av_frame_alloc();

                ffmpeg.av_frame_move_ref(keep, frame);
            }

            Interlocked.Increment(ref _framesDecoded);

            Frames.Add(new VideoFrame { Frame = keep, Time = time, Duration = duration, IsHardware = hardware, Number = ++state.Number, Width = keep->width, Height = keep->height }, () => Clock, _token);

            if (!_videoReady)
            {
                _videoReady = true;

                PlaybackTrace.Mark("first picture decoded", $"{keep->width}x{keep->height}");
            }
        }

        private unsafe void AudioLoop()
        {
            AudioDecoder decoder = null;
            var frame = ffmpeg.av_frame_alloc();
            var resampler = new Resampler(AudioOutput.Rate);
            var state = new AudioState();

            try
            {
                while (!_token.IsCancellationRequested)
                {
                    var item = _audioPackets.Take(_token);

                    if (item == null)
                    {
                        break;
                    }

                    try
                    {
                        switch (item.Kind)
                        {
                            case PacketKind.Reconfigure:
                            {
                                DrainAudio(decoder, frame, resampler, state);

                                decoder?.Dispose();
                                decoder = null;

                                try
                                {
                                    decoder = AudioDecoder.Open(item.Parameters);

                                    TvCore.LogInfo($"[Player] Session {Id}: audio decoder {decoder.CodecName}");
                                }
                                catch (Exception ex)
                                {
                                    TvCore.LogError($"[Player] Session {Id}: no sound, {ex.Message}");
                                }
                            }
                            break;

                            case PacketKind.Drain:
                            {
                                DrainAudio(decoder, frame, resampler, state);

                                decoder?.Flush();
                            }
                            break;

                            case PacketKind.End:
                            {
                                DrainAudio(decoder, frame, resampler, state);

                                return;
                            }

                            case PacketKind.Packet:
                            {
                                if (decoder == null)
                                {
                                    break;
                                }

                                var sent = decoder.Send(item.Packet);

                                if (sent == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                                {
                                    ReceiveAudio(decoder, frame, resampler, state);

                                    decoder.Send(item.Packet);
                                }

                                ReceiveAudio(decoder, frame, resampler, state);
                            }
                            break;
                        }
                    }
                    finally
                    {
                        item.Free();
                    }
                }
            }
            finally
            {
                _audioDone = true;

                decoder?.Dispose();
                resampler.Dispose();

                ffmpeg.av_frame_free(&frame);
            }
        }

        private sealed class AudioState
        {
            public float[] Buffer;

            public double Next = double.NaN;

            public bool Logged;
        }

        private unsafe void DrainAudio(AudioDecoder decoder, AVFrame* frame, Resampler resampler, AudioState state)
        {
            if (decoder == null)
            {
                return;
            }

            decoder.Send(null);

            ReceiveAudio(decoder, frame, resampler, state);
        }

        private unsafe void ReceiveAudio(AudioDecoder decoder, AVFrame* frame, Resampler resampler, AudioState state)
        {
            while (!_token.IsCancellationRequested && decoder.Receive(frame) >= 0)
            {
                try
                {
                    OnAudioFrame(decoder, frame, resampler, state);
                }
                finally
                {
                    ffmpeg.av_frame_unref(frame);
                }
            }
        }

        private unsafe void OnAudioFrame(AudioDecoder decoder, AVFrame* frame, Resampler resampler, AudioState state)
        {
            if (frame->nb_samples <= 0 || frame->sample_rate <= 0)
            {
                return;
            }

            var channels = _owner.StereoMode == StereoMode.Surround && frame->ch_layout.nb_channels >= 6 ? 6 : 2;

            if (!_audio.Open(channels))
            {
                return;
            }

            var count = resampler.Convert(frame, channels, ref state.Buffer);

            if (count <= 0)
            {
                return;
            }

            PostProcess(state.Buffer, count, channels, _owner.StereoMode);

            var stamp = frame->best_effort_timestamp;
            var time = stamp != ffmpeg.AV_NOPTS_VALUE ? stamp / (double)ffmpeg.AV_TIME_BASE : double.IsNaN(state.Next) ? 0 : state.Next;

            if (!double.IsNaN(state.Next) && Math.Abs(time - state.Next) < 0.15)
            {
                time = state.Next;
            }

            if (!state.Logged)
            {
                state.Logged = true;

                TvCore.LogInfo($"[Player] Session {Id}: first sound {decoder.CodecName} {frame->ch_layout.nb_channels}ch {frame->sample_rate}Hz at {time:0.000}s");
            }

            SetInfo(x =>
            {
                x.AudioCodec = decoder.CodecName;
                x.AudioChannels = frame->ch_layout.nb_channels;
                x.AudioRate = frame->sample_rate;
            });

            while (!_token.IsCancellationRequested && _audio.QueuedSeconds > AudioAheadSeconds)
            {
                Thread.Sleep(5);
            }

            if (_token.IsCancellationRequested || !_audio.Write(_audioSession, state.Buffer, count, time))
            {
                return;
            }

            state.Next = time + count / (double)AudioOutput.Rate;

            if (!_audioReady && _audio.QueuedSeconds >= StartAudioSeconds)
            {
                _audioReady = true;

                PlaybackTrace.Mark("sound ready");
            }
        }

        private static void PostProcess(float[] samples, int frames, int channels, StereoMode mode)
        {
            const float gain = 1.8f;

            var total = frames * channels;

            if (channels == 2)
            {
                for (var i = 0; i < total; i += 2)
                {
                    var left = samples[i];
                    var right = samples[i + 1];

                    switch (mode)
                    {
                        case StereoMode.ReverseStereo:
                        {
                            samples[i] = right;
                            samples[i + 1] = left;
                        }
                        break;

                        case StereoMode.Left:
                        {
                            samples[i + 1] = left;
                        }
                        break;

                        case StereoMode.Right:
                        {
                            samples[i] = right;
                        }
                        break;

                        case StereoMode.Mono:
                        {
                            samples[i] = samples[i + 1] = (left + right) * 0.5f;
                        }
                        break;
                    }
                }
            }

            for (var i = 0; i < total; i++)
            {
                var value = samples[i] * gain;

                samples[i] = value > 1 ? 1 : value < -1 ? -1 : value;
            }
        }

        private void SetInfo(Action<StreamInfo> change)
        {
            StreamInfo changed = null;

            lock (_infoLock)
            {
                var before = _info.Clone();

                change(_info);

                if (!_info.Equals(before))
                {
                    changed = _info.Clone();
                }
            }

            if (changed != null)
            {
                _owner.ReportInfo(this, changed);
            }
        }

        public VideoFrame TakeFrame(double lead)
        {
            if (_token.IsCancellationRequested)
            {
                return null;
            }

            var clock = Clock;

            if (double.IsNaN(clock))
            {
                if (_posterTaken)
                {
                    return null;
                }

                var poster = Frames.TakeFirst();

                if (poster != null)
                {
                    _posterTaken = true;

                    Interlocked.Increment(ref _framesShown);
                }

                return poster;
            }

            var due = Frames.TakeDue(clock + lead);

            if (due != null)
            {
                Interlocked.Increment(ref _framesShown);
            }

            return due;
        }

        private void Monitor()
        {
            if (_token.IsCancellationRequested || Interlocked.Exchange(ref _monitoring, 1) == 1)
            {
                return;
            }

            try
            {
                if (!_started)
                {
                    TryStart();
                }
                else
                {
                    CheckBuffering();
                    FireMarks();
                    CheckEnd();
                }

                _owner.ReportBuffer(this, (int)Math.Min(100, BufferedSeconds() / RefillTarget * 100));
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Player] Session {Id} monitor: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _monitoring, 0);
            }
        }

        private void TryStart()
        {
            if (!_videoReady && !_audioReady)
            {
                return;
            }

            if (double.IsNaN(_readySince))
            {
                _readySince = _age.Elapsed.TotalSeconds;
            }

            var known = _demuxersStarted > 0 && Volatile.Read(ref _demuxersOpened) >= _demuxersStarted;
            var videoOk = _videoReady || (known && !_expectVideo) || _videoDone;
            var audioOk = _audioReady || (known && !_expectAudio) || _audioDone;
            var waited = _age.Elapsed.TotalSeconds - _readySince;

            if (!(videoOk && audioOk) && waited < 3)
            {
                return;
            }

            if (!(videoOk && audioOk))
            {
                TvCore.LogError($"[Player] Session {Id}: starting without {(videoOk ? "sound" : "pictures")}, it did not arrive within 3s");
            }

            _started = true;

            if (_audioReady)
            {
                _audioClock = true;

                _audio.Resume();
            }
            else
            {
                _audioClock = false;

                _wall.Start(double.IsNaN(Frames.FirstTime) ? 0 : Frames.FirstTime);
            }

            PlaybackTrace.Mark("playing", _audioClock ? "timed by the sound" : "timed by the wall clock");

            _owner.Report(this, PlayerState.Playing, null);
        }

        private bool Starving()
        {
            if (_audioClock)
            {
                return !_audioDone && _audioPackets.Count == 0 && _audio.QueuedSeconds < 0.05;
            }

            return !_videoDone && Frames.Count == 0 && _videoPackets.Count == 0;
        }

        private void CheckBuffering()
        {
            if (!_buffering)
            {
                if (!Starving())
                {
                    return;
                }

                _buffering = true;

                Interlocked.Increment(ref _underruns);

                if (_audioClock)
                {
                    _audio.Pause();
                }
                else
                {
                    _wall.Pause();
                }

                TvCore.LogError($"[Player] Session {Id}: ran dry at {Clock:0.000}s, buffering. {Stats}");

                _owner.Report(this, PlayerState.Buffering, null);

                return;
            }

            var sourceDone = _demuxersRunning == 0;

            if (!sourceDone && BufferedSeconds() < RefillTarget)
            {
                return;
            }

            if (_audioClock && _audio.QueuedSeconds < StartAudioSeconds && !_audioDone)
            {
                return;
            }

            _buffering = false;

            if (_audioClock)
            {
                _audio.Resume();
            }
            else
            {
                _wall.Start(0);
            }

            TvCore.LogInfo($"[Player] Session {Id}: buffer refilled, playing again at {Clock:0.000}s");

            _owner.Report(this, PlayerState.Playing, null);
        }

        private void FireMarks()
        {
            var clock = Clock;

            if (double.IsNaN(clock))
            {
                return;
            }

            List<MediaChunk> pieces = null;

            lock (_pieceMarks)
            {
                while (_pieceMarks.Count > 0 && _pieceMarks[0].Item1 <= clock)
                {
                    (pieces ??= new List<MediaChunk>()).Add(_pieceMarks[0].Item2);

                    _pieceMarks.RemoveAt(0);
                }
            }

            if (pieces != null)
            {
                foreach (var piece in pieces)
                {
                    AdDetector.ObserveSegment(piece.Url);

                    if (piece.StartsDiscontinuity)
                    {
                        AdDetector.ObserveDiscontinuity();
                    }
                }
            }

            string caption = null;
            var changed = false;

            lock (_captionMarks)
            {
                while (_captionMarks.Count > 0 && _captionMarks[0].Item1 <= clock)
                {
                    caption = _captionMarks[0].Item2;
                    changed = true;

                    _captionMarks.RemoveAt(0);
                }
            }

            if (changed && caption != _lastCaption)
            {
                _lastCaption = caption;

                _owner.ReportCaption(this, caption);
            }
        }

        private void CheckEnd()
        {
            if (_finished || _demuxersRunning > 0 || !_videoDone && _expectVideo || !_audioDone && _expectAudio)
            {
                return;
            }

            if (Frames.Count > 0 || (_audioClock && _audio.QueuedSeconds > 0.02))
            {
                return;
            }

            _finished = true;

            TvCore.LogInfo($"[Player] Session {Id}: played to the end. {Stats}");

            PlaybackTrace.Mark("end reached");

            _owner.Report(this, PlayerState.Ended, null);
        }
    }
}
