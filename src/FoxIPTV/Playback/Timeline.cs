// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System.Collections.Generic;
    using Classes;

    public sealed class Timeline
    {
        private const double BackwardJump = 1.0;

        private const double ForwardJump = 10.0;

        private readonly object _lock = new object();

        private readonly Dictionary<int, double> _offsets = new Dictionary<int, double>();

        private double _end = double.NaN;

        private int _synthetic = -1;

        public double Offset(int key, double seconds)
        {
            lock (_lock)
            {
                if (!_offsets.TryGetValue(key, out var offset))
                {
                    offset = (double.IsNaN(_end) ? 0 : _end) - seconds;

                    _offsets[key] = offset;

                    TvCore.LogInfo($"[Player] Timeline: run {key} starts at source {seconds:0.000}s, plays from {seconds + offset:0.000}s");
                }

                return offset;
            }
        }

        public void NoteEnd(double time)
        {
            lock (_lock)
            {
                if (double.IsNaN(_end) || time > _end)
                {
                    _end = time;
                }
            }
        }

        public bool IsJump(double mapped, double last)
        {
            return !double.IsNaN(last) && (mapped < last - BackwardJump || mapped > last + ForwardJump);
        }

        public int NewRun()
        {
            lock (_lock)
            {
                return _synthetic--;
            }
        }
    }
}
