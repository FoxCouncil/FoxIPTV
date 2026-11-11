// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests.Engine
{
    using FoxIPTV.Playback;

    public sealed class DrainWatchTests
    {
        [Fact]
        public void SoundTakenInRealTimeIsNotAFault()
        {
            var watch = new DrainWatch();

            for (var tick = 0; tick <= 100; tick++)
            {
                var now = tick * 0.1;

                Assert.False(watch.Observe(now, now, 1), $"fault at {now:0.0}s");
            }
        }

        [Fact]
        public void TheLiveSpeedTrimIsNotAFault()
        {
            var watch = new DrainWatch();

            for (var tick = 0; tick <= 100; tick++)
            {
                var now = tick * 0.1;

                Assert.False(watch.Observe(now, now * 1.05, 1.05), $"fault at {now:0.0}s");
            }
        }

        [Fact]
        public void TheRateFromTheMacBookLogIsAFault()
        {
            var watch = new DrainWatch();

            Assert.False(watch.Observe(0, 0, 1));
            Assert.True(watch.Observe(0.822, 4.572, 1));
            Assert.Equal(4.572, watch.Drained, 3);
            Assert.Equal(0.822, watch.Elapsed, 3);
        }

        [Fact]
        public void AFaultNeedsHalfASecondOfWatching()
        {
            var watch = new DrainWatch();

            Assert.False(watch.Observe(0, 0, 1));
            Assert.False(watch.Observe(0.3, 1.8, 1));
            Assert.True(watch.Observe(0.6, 3.6, 1));
        }

        [Fact]
        public void AResetStartsTheWatchAgain()
        {
            var watch = new DrainWatch();

            Assert.False(watch.Observe(0, 0, 1));

            watch.Reset();

            Assert.False(watch.Observe(0.6, 3.6, 1));
            Assert.False(watch.Observe(1.2, 4.2, 1));
        }
    }
}
