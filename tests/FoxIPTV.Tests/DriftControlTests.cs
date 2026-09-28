// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using FoxIPTV.Playback;

    public class DriftControlTests
    {
        private const double Tick = 0.1;

        private const int TicksPerHour = 36000;

        private const int TicksPerPiece = 60;

        private const double PieceSeconds = TicksPerPiece * Tick;

        private static (double Lowest, double LastHourSpeed, DriftControl Control) PlayLive(double soundCardSpeed, int hours, bool steer)
        {
            var control = new DriftControl();
            var loaded = 18.0;
            var played = 0.0;
            var speed = 1.0;
            var lowest = double.MaxValue;
            var ticks = hours * TicksPerHour;
            var lastHourSum = 0.0;

            for (var tick = 1; tick <= ticks; tick++)
            {
                if (tick % TicksPerPiece == 0)
                {
                    loaded += PieceSeconds;
                }

                played += Tick * soundCardSpeed * speed;

                var buffered = loaded - played;

                lowest = Math.Min(lowest, buffered);

                var steered = control.Update(Tick, buffered);

                if (steer)
                {
                    speed = steered;
                }

                if (tick > ticks - TicksPerHour)
                {
                    lastHourSum += speed;
                }
            }

            return (lowest, lastHourSum / TicksPerHour, control);
        }

        [Fact]
        public void FastSoundCard_DrainsTheBufferWithoutSteering()
        {
            var (lowest, _, _) = PlayLive(1.0002, 12, false);

            Assert.True(lowest < 12 - 8, $"lowest buffer {lowest:0.00}s");
        }

        [Fact]
        public void FastSoundCard_BufferHeldWhenSteering()
        {
            var (lowest, speed, control) = PlayLive(1.0002, 12, true);

            Assert.True(control.IsHolding);
            Assert.True(Math.Abs(control.Average - control.Target) < 0.4, $"average {control.Average:0.00}s, target {control.Target:0.00}s");
            Assert.True(Math.Abs(speed * 1.0002 - 1) < 0.000005, $"speed {speed:0.0000000}");
            Assert.True(lowest > 11, $"lowest buffer {lowest:0.00}s");
        }

        [Fact]
        public void SlowSoundCard_BufferHeldWhenSteering()
        {
            var (_, speed, control) = PlayLive(0.9999, 6, true);

            Assert.True(Math.Abs(control.Average - control.Target) < 0.4, $"average {control.Average:0.00}s, target {control.Target:0.00}s");
            Assert.True(Math.Abs(speed * 0.9999 - 1) < 0.000005, $"speed {speed:0.0000000}");
        }

        [Fact]
        public void Steering_WaitsThenCapsTheCorrection()
        {
            var control = new DriftControl();

            for (var now = 0.0; now < DriftControl.HoldAfterSeconds - 1; now += Tick)
            {
                Assert.Equal(1.0, control.Update(Tick, 10));
            }

            Assert.False(control.IsHolding);

            for (var now = 0.0; now < 2; now += Tick)
            {
                control.Update(Tick, 10);
            }

            Assert.True(control.IsHolding);
            Assert.Equal(10, control.Target, 6);

            for (var now = 0.0; now < 600; now += Tick)
            {
                control.Update(Tick, 100);
            }

            Assert.Equal(1 + DriftControl.MaxCorrection, control.Speed, 9);

            for (var now = 0.0; now < 1200; now += Tick)
            {
                control.Update(Tick, 0);
            }

            Assert.Equal(1 - DriftControl.MaxCorrection, control.Speed, 9);
        }

        [Fact]
        public void WallClock_SpeedChangeKeepsTimeContinuous()
        {
            var clock = new WallClock();

            clock.Start(5);

            Thread.Sleep(50);

            var before = clock.Now;

            clock.Speed = 2;

            var after = clock.Now;
            var watch = Stopwatch.StartNew();

            Assert.True(Math.Abs(after - before) < 0.01, $"jumped {after - before:0.000}s");

            Thread.Sleep(200);

            var advanced = clock.Now - after;
            var real = watch.Elapsed.TotalSeconds;

            Assert.True(Math.Abs(advanced - real * 2) < 0.02, $"advanced {advanced:0.000}s in {real:0.000}s");
        }
    }
}
