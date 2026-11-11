// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using FoxIPTV.Classes;

    public sealed class StaleWatchTests
    {
        private static readonly DateTime Start = new DateTime(2026, 11, 11, 6, 49, 18, DateTimeKind.Utc);

        [Fact]
        public void PlayThatKeepsMovingIsNotStale()
        {
            var watch = new StaleWatch();

            for (var tick = 0; tick <= 6000; tick++)
            {
                var seconds = tick * 0.1;

                Assert.False(watch.ClockMoved(Start.AddSeconds(seconds), 191 + seconds * 0.995), $"stale at {seconds:0.0}s");
            }
        }

        [Fact]
        public void TheWakeFromTheMacBookLogIsStale()
        {
            var watch = new StaleWatch();

            Assert.False(watch.ClockMoved(Start, 191.397));
            Assert.True(watch.ClockMoved(Start.AddSeconds(1834), 191.991));
            Assert.Equal(1833.4, watch.StoodStill.TotalSeconds, 1);
        }

        [Fact]
        public void ALateTimerWhilePlayWentOnIsNotStale()
        {
            var watch = new StaleWatch();

            Assert.False(watch.ClockMoved(Start, 191));
            Assert.False(watch.ClockMoved(Start.AddSeconds(40), 231));
        }

        [Fact]
        public void AShortStopIsNotStale()
        {
            var watch = new StaleWatch();

            Assert.False(watch.ClockMoved(Start, 191.394));
            Assert.False(watch.ClockMoved(Start.AddSeconds(12.7), 191.397));
        }

        [Fact]
        public void AResetStartsTheWatchAgain()
        {
            var watch = new StaleWatch();

            Assert.False(watch.ClockMoved(Start, 191));

            watch.Reset();

            Assert.False(watch.ClockMoved(Start.AddSeconds(1834), 0.5));
            Assert.False(watch.ClockMoved(Start.AddSeconds(1835), 1.5));
        }
    }
}
