// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using FFmpeg.AutoGen;

    public enum PacketKind
    {
        Packet,
        Reconfigure,
        Drain,
        End
    }

    public sealed unsafe class PacketItem
    {
        public PacketKind Kind { get; set; }

        public AVPacket* Packet { get; set; }

        public AVCodecParameters* Parameters { get; set; }

        public double Duration { get; set; }

        public void Free()
        {
            var packet = Packet;
            var parameters = Parameters;

            if (packet != null)
            {
                ffmpeg.av_packet_free(&packet);
            }

            if (parameters != null)
            {
                ffmpeg.avcodec_parameters_free(&parameters);
            }

            Packet = null;
            Parameters = null;
        }
    }

    public sealed class PacketQueue
    {
        private readonly object _lock = new object();

        private readonly Queue<PacketItem> _items = new Queue<PacketItem>();

        private double _seconds;

        public PacketQueue(double maxSeconds, int maxCount)
        {
            MaxSeconds = maxSeconds;
            MaxCount = maxCount;
        }

        public double MaxSeconds { get; }

        public int MaxCount { get; }

        public double Seconds
        {
            get
            {
                lock (_lock)
                {
                    return _seconds;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _items.Count;
                }
            }
        }

        public void Add(PacketItem item, CancellationToken token)
        {
            lock (_lock)
            {
                while (item.Kind == PacketKind.Packet && _items.Count > 0 && (_seconds >= MaxSeconds || _items.Count >= MaxCount))
                {
                    if (token.IsCancellationRequested)
                    {
                        item.Free();

                        token.ThrowIfCancellationRequested();
                    }

                    Monitor.Wait(_lock, 50);
                }

                _items.Enqueue(item);
                _seconds += item.Duration;

                Monitor.PulseAll(_lock);
            }
        }

        public PacketItem Take(CancellationToken token)
        {
            lock (_lock)
            {
                while (_items.Count == 0)
                {
                    if (token.IsCancellationRequested)
                    {
                        return null;
                    }

                    Monitor.Wait(_lock, 50);
                }

                var item = _items.Dequeue();

                _seconds = _items.Count == 0 ? 0 : Math.Max(0, _seconds - item.Duration);

                Monitor.PulseAll(_lock);

                return item;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                while (_items.Count > 0)
                {
                    _items.Dequeue().Free();
                }

                _seconds = 0;

                Monitor.PulseAll(_lock);
            }
        }
    }

    public sealed unsafe class VideoFrame
    {
        public AVFrame* Frame { get; set; }

        public double Time { get; set; }

        public double Duration { get; set; }

        public bool IsHardware { get; set; }

        public long Number { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public double DisplayAspect(double forcedAspect)
        {
            if (forcedAspect > 0)
            {
                return forcedAspect;
            }

            var sar = Frame->sample_aspect_ratio;
            var pixelAspect = sar.num > 0 && sar.den > 0 ? sar.num / (double)sar.den : 1.0;

            return Frame->width * pixelAspect / Math.Max(1, Frame->height);
        }

        public void Free()
        {
            var frame = Frame;

            if (frame != null)
            {
                ffmpeg.av_frame_free(&frame);
            }

            Frame = null;
        }
    }

    public sealed class FrameQueue
    {
        private readonly object _lock = new object();

        private readonly LinkedList<VideoFrame> _items = new LinkedList<VideoFrame>();

        public FrameQueue(int capacity)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }

        public long Dropped { get; private set; }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _items.Count;
                }
            }
        }

        public void Add(VideoFrame frame, Func<double> clock, CancellationToken token)
        {
            lock (_lock)
            {
                while (_items.Count >= Capacity)
                {
                    if (token.IsCancellationRequested)
                    {
                        frame.Free();

                        return;
                    }

                    var now = clock();

                    if (!double.IsNaN(now))
                    {
                        while (_items.Count > 1 && _items.First.Value.Time + _items.First.Value.Duration < now - 0.05)
                        {
                            _items.First.Value.Free();
                            _items.RemoveFirst();

                            Dropped++;
                        }
                    }

                    if (_items.Count < Capacity)
                    {
                        break;
                    }

                    Monitor.Wait(_lock, 10);
                }

                _items.AddLast(frame);
            }
        }

        public VideoFrame TakeDue(double target)
        {
            lock (_lock)
            {
                VideoFrame due = null;

                while (_items.First != null && _items.First.Value.Time <= target)
                {
                    if (due != null)
                    {
                        due.Free();

                        Dropped++;
                    }

                    due = _items.First.Value;

                    _items.RemoveFirst();
                }

                if (due != null)
                {
                    Monitor.PulseAll(_lock);
                }

                return due;
            }
        }

        public VideoFrame TakeFirst()
        {
            lock (_lock)
            {
                var first = _items.First?.Value;

                if (first != null)
                {
                    _items.RemoveFirst();

                    Monitor.PulseAll(_lock);
                }

                return first;
            }
        }

        public double FirstTime
        {
            get
            {
                lock (_lock)
                {
                    return _items.First?.Value.Time ?? double.NaN;
                }
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                Monitor.PulseAll(_lock);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                foreach (var item in _items)
                {
                    item.Free();
                }

                _items.Clear();

                Monitor.PulseAll(_lock);
            }
        }
    }
}
