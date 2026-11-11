// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;

    public sealed class StaleWatch
    {
        public static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

        private DateTime? _movedAt;

        private double _clock;

        public TimeSpan StoodStill { get; private set; }

        public void Reset()
        {
            _movedAt = null;
        }

        public bool ClockMoved(DateTime utcNow, double clock)
        {
            StoodStill = _movedAt.HasValue ? utcNow - _movedAt.Value - TimeSpan.FromSeconds(Math.Max(0, clock - _clock)) : TimeSpan.Zero;

            _movedAt = utcNow;
            _clock = clock;

            return StoodStill >= Limit;
        }
    }
}
