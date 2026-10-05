// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Classes;
    using FFmpeg.AutoGen;
    using Hls;

    public sealed class OpenedSource : IDisposable
    {
        public HlsPlaylist Playlist { get; set; }

        public ProgressiveReader Reader { get; set; }

        public string Url { get; set; }

        public void Dispose()
        {
            Reader?.Dispose();
        }

        public static async Task<OpenedSource> Open(MediaRequest request, CancellationToken token)
        {
            var scheme = request.Uri.Scheme.ToLowerInvariant();

            if (scheme != "http" && scheme != "https")
            {
                return new OpenedSource { Url = request.Uri.IsFile ? request.Uri.LocalPath : request.Uri.ToString() };
            }

            HttpResponseMessage response;

            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(15));

                try
                {
                    response = await MediaHttp.Send(request.Uri, request.Headers, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    throw new TimeoutException("The stream did not answer within 15s");
                }
            }

            try
            {
                var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                var peek = new byte[4096];
                var filled = 0;

                while (filled < 16)
                {
                    var read = await stream.ReadAsync(peek.AsMemory(filled, peek.Length - filled), token).ConfigureAwait(false);

                    if (read == 0)
                    {
                        break;
                    }

                    filled += read;
                }

                var type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                var head = Encoding.UTF8.GetString(peek, 0, filled);
                var finalUri = response.RequestMessage?.RequestUri ?? request.Uri;

                if (HlsPlaylist.LooksLikePlaylist(head) || type.IndexOf("mpegurl", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    using (var all = new MemoryStream())
                    {
                        all.Write(peek, 0, filled);

                        await stream.CopyToAsync(all, token).ConfigureAwait(false);

                        var playlist = HlsPlaylist.Parse(Web.Decode(all.ToArray()), finalUri);

                        response.Dispose();

                        return new OpenedSource { Playlist = playlist };
                    }
                }

                var reader = new ProgressiveReader(request, response, stream, peek.Take(filled).ToArray(), token);

                return new OpenedSource { Reader = reader };
            }
            catch
            {
                response.Dispose();

                throw;
            }
        }
    }

    public sealed unsafe class ProgressiveReader : IDisposable
    {
        private const int AvseekSize = 0x10000;

        private const int AvseekForce = 0x20000;

        private readonly MediaRequest _request;

        private readonly CancellationToken _token;

        private HttpResponseMessage _response;

        private Stream _stream;

        private byte[] _pending;

        private int _pendingPosition;

        private long _position;

        private int _reconnects;

        private byte[] _buffer = new byte[65536];

        public ProgressiveReader(MediaRequest request, HttpResponseMessage response, Stream stream, byte[] peeked, CancellationToken token)
        {
            _request = request;
            _response = response;
            _stream = stream;
            _pending = peeked;
            _token = token;

            Length = response.Content.Headers.ContentLength;
            CanSeek = Length.HasValue && Length.Value > 0 && response.Headers.AcceptRanges.Contains("bytes");

            TvCore.LogInfo($"[Player] Plain stream {response.Content.Headers.ContentType?.MediaType ?? "no type"}, {(Length.HasValue ? $"{Length.Value / 1048576.0:0.0}MB" : "no length")}, {(CanSeek ? "seekable" : "not seekable")}");
        }

        public long? Length { get; }

        public bool CanSeek { get; }

        public Action<int> OnBytes { get; set; }

        public int Read(byte* buffer, int size)
        {
            while (true)
            {
                if (_token.IsCancellationRequested)
                {
                    return ffmpeg.AVERROR_EXIT;
                }

                if (_pending != null && _pendingPosition < _pending.Length)
                {
                    var count = Math.Min(size, _pending.Length - _pendingPosition);

                    Marshal.Copy(_pending, _pendingPosition, (IntPtr)buffer, count);

                    _pendingPosition += count;

                    return Advance(count);
                }

                _pending = null;

                if (Length.HasValue && _position >= Length.Value)
                {
                    return ffmpeg.AVERROR_EOF;
                }

                try
                {
                    if (_buffer.Length < size)
                    {
                        _buffer = new byte[size];
                    }

                    var read = _stream.ReadAsync(_buffer, 0, size, _token).GetAwaiter().GetResult();

                    if (read > 0)
                    {
                        Marshal.Copy(_buffer, 0, (IntPtr)buffer, read);

                        _reconnects = 0;

                        return Advance(read);
                    }

                    if (!Reconnect("the stream ended"))
                    {
                        return ffmpeg.AVERROR_EOF;
                    }
                }
                catch (OperationCanceledException)
                {
                    return ffmpeg.AVERROR_EXIT;
                }
                catch (Exception ex) when (ex is IOException || ex is HttpRequestException)
                {
                    if (!Reconnect(ex.Message))
                    {
                        return ffmpeg.AVERROR(5);
                    }
                }
            }
        }

        public long Seek(long offset, int whence)
        {
            whence &= ~AvseekForce;

            if (whence == AvseekSize)
            {
                return Length ?? -1;
            }

            if (!CanSeek)
            {
                return -1;
            }

            long target;

            switch (whence)
            {
                case 0:
                {
                    target = offset;
                }
                break;

                case 1:
                {
                    target = _position + offset;
                }
                break;

                case 2:
                {
                    target = Length.Value + offset;
                }
                break;

                default:
                {
                    return -1;
                }
            }

            if (target < 0 || target > Length.Value)
            {
                return -1;
            }

            if (target == _position)
            {
                return target;
            }

            try
            {
                Open(target);
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Player] Seek to byte {target} failed: {ex.Message}");

                return -1;
            }

            return target;
        }

        private int Advance(int count)
        {
            _position += count;

            OnBytes?.Invoke(count);

            return count;
        }

        private bool Reconnect(string why)
        {
            if (_token.IsCancellationRequested || _reconnects >= 5 || (Length.HasValue && !CanSeek) || (!_request.IsLive && !CanSeek))
            {
                TvCore.LogError($"[Player] Plain stream stopped at byte {_position}: {why}");

                return false;
            }

            _reconnects++;

            TvCore.LogError($"[Player] Plain stream broke at byte {_position} ({why}), reconnecting, try {_reconnects}");

            try
            {
                Thread.Sleep(Math.Min(4000, 500 * _reconnects));

                Open(CanSeek ? _position : (long?)null);

                return true;
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Player] Reconnect failed: {ex.Message}");

                return _reconnects < 5 && Reconnect(ex.Message);
            }
        }

        private void Open(long? from)
        {
            Close();

            _response = MediaHttp.Send(_request.Uri, _request.Headers, _token, from).GetAwaiter().GetResult();
            _stream = _response.Content.ReadAsStream(_token);

            if (from.HasValue)
            {
                _position = from.Value;
            }
        }

        private void Close()
        {
            try
            {
                _stream?.Dispose();
                _response?.Dispose();
            }
            catch (Exception)
            {
            }

            _stream = null;
            _response = null;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
