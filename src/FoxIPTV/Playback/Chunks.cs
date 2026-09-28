// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
    using FFmpeg.AutoGen;

    public sealed class MediaChunk
    {
        public byte[] Data { get; set; }

        public int Period { get; set; }

        public int Discontinuity { get; set; }

        public bool IsInit { get; set; }

        public double Duration { get; set; }

        public string Url { get; set; }

        public bool StartsDiscontinuity { get; set; }

        public override string ToString() => $"period {Period} d{Discontinuity}{(IsInit ? " init" : string.Empty)} {Data?.Length ?? 0}B {Duration:0.000}s {Url}";
    }

    public sealed class ChunkQueue
    {
        private readonly object _lock = new object();

        private readonly LinkedList<MediaChunk> _items = new LinkedList<MediaChunk>();

        private double _seconds;

        private long _bytes;

        private bool _completed;

        public double MaxSeconds { get; set; } = 120;

        public long MaxBytes { get; set; } = 128L * 1024 * 1024;

        public double BufferedSeconds
        {
            get
            {
                lock (_lock)
                {
                    return _seconds;
                }
            }
        }

        public bool IsCompleted
        {
            get
            {
                lock (_lock)
                {
                    return _completed && _items.Count == 0;
                }
            }
        }

        public bool IsFinished
        {
            get
            {
                lock (_lock)
                {
                    return _completed;
                }
            }
        }

        public void Add(MediaChunk chunk, CancellationToken token)
        {
            lock (_lock)
            {
                while (!_completed && _items.Count > 0 && (_seconds >= MaxSeconds || _bytes >= MaxBytes))
                {
                    token.ThrowIfCancellationRequested();

                    Monitor.Wait(_lock, 100);
                }

                token.ThrowIfCancellationRequested();

                _items.AddLast(chunk);
                _seconds += chunk.Duration;
                _bytes += chunk.Data?.Length ?? 0;

                Monitor.PulseAll(_lock);
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                _completed = true;

                Monitor.PulseAll(_lock);
            }
        }

        public MediaChunk Peek(CancellationToken token)
        {
            lock (_lock)
            {
                while (_items.Count == 0 && !_completed)
                {
                    if (token.IsCancellationRequested)
                    {
                        return null;
                    }

                    Monitor.Wait(_lock, 100);
                }

                return _items.First?.Value;
            }
        }

        public MediaChunk Take()
        {
            lock (_lock)
            {
                var first = _items.First;

                if (first == null)
                {
                    return null;
                }

                _items.RemoveFirst();
                _seconds = Math.Max(0, _seconds - first.Value.Duration);
                _bytes -= first.Value.Data?.Length ?? 0;

                Monitor.PulseAll(_lock);

                return first.Value;
            }
        }
    }

    public sealed unsafe class ChunkReader
    {
        private readonly ChunkQueue _queue;

        private readonly CancellationToken _token;

        private readonly List<MediaChunk> _started = new List<MediaChunk>();

        private MediaChunk _current;

        private int _position;

        public ChunkReader(ChunkQueue queue, int period, CancellationToken token)
        {
            _queue = queue;
            _token = token;

            Period = period;
        }

        public int Period { get; }

        public MediaChunk First { get; private set; }

        public long BytesRead { get; private set; }

        public List<MediaChunk> TakeStarted()
        {
            lock (_started)
            {
                if (_started.Count == 0)
                {
                    return null;
                }

                var started = new List<MediaChunk>(_started);

                _started.Clear();

                return started;
            }
        }

        public int Read(byte* buffer, int size)
        {
            while (true)
            {
                if (_token.IsCancellationRequested)
                {
                    return ffmpeg.AVERROR_EXIT;
                }

                if (_current != null && _position < _current.Data.Length)
                {
                    var count = Math.Min(size, _current.Data.Length - _position);

                    Marshal.Copy(_current.Data, _position, (IntPtr)buffer, count);

                    _position += count;
                    BytesRead += count;

                    return count;
                }

                var next = _queue.Peek(_token);

                if (next == null)
                {
                    return _token.IsCancellationRequested ? ffmpeg.AVERROR_EXIT : ffmpeg.AVERROR_EOF;
                }

                if (next.Period != Period)
                {
                    return ffmpeg.AVERROR_EOF;
                }

                _queue.Take();

                _current = next;
                _position = 0;

                First ??= next;

                lock (_started)
                {
                    _started.Add(next);
                }
            }
        }
    }
}
