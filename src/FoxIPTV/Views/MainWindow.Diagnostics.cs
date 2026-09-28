// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using Avalonia.Controls;
    using Classes;

    public partial class MainWindow
    {
        private static readonly TimeSpan ClockStallThreshold = TimeSpan.FromSeconds(3);

        private static readonly TimeSpan UiLateThreshold = TimeSpan.FromMilliseconds(750);

        private const double HeartbeatSeconds = 30;

        private double _lastClock = double.NaN;

        private long _clockMovedAt;

        private long _stallReportedAt;

        private long _lastTickAt;

        private double _heartbeatClock = double.NaN;

        private static long Now => Stopwatch.GetTimestamp();

        private static string Time(double seconds)
        {
            return double.IsNaN(seconds) ? "none" : TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss\.fff");
        }

        private void ResetDiagnostics()
        {
            _lastClock = double.NaN;
            _clockMovedAt = 0;
            _stallReportedAt = 0;
            _heartbeatClock = double.NaN;
        }

        private void Watchdog()
        {
            var now = Now;
            var lastTick = _lastTickAt;

            _lastTickAt = now;

            if (lastTick != 0)
            {
                var gap = Stopwatch.GetElapsedTime(lastTick, now);

                if (gap >= UiLateThreshold)
                {
                    TvCore.LogError($"[Watchdog] UI thread was busy for {gap.TotalMilliseconds:0}ms (timer due every 100ms)");
                }
            }

            var clock = _player.Clock;

            if (!_isPlaying || double.IsNaN(clock))
            {
                return;
            }

            if (double.IsNaN(_lastClock) || clock > _lastClock + 0.001)
            {
                if (_stallReportedAt != 0)
                {
                    TvCore.LogError($"[Watchdog] Clock moving again at {Time(clock)} after {Stopwatch.GetElapsedTime(_clockMovedAt, now).TotalSeconds:0.0}s stuck at {Time(_lastClock)}; {_player.Stats}");

                    _stallReportedAt = 0;
                }

                _lastClock = clock;
                _clockMovedAt = now;

                var time = TimeSpan.FromSeconds(clock);

                TimeStatusLabel.Text = $"{time.Minutes:00}:{time.Seconds:00}";

                if (double.IsNaN(_heartbeatClock) || clock - _heartbeatClock >= HeartbeatSeconds)
                {
                    _heartbeatClock = clock;

                    TvCore.LogInfo($"[Clock] {Time(clock)}, {_player.Stats}, captions {(TvCore.Settings.CCEnabled ? "on" : "off")}{(_ccAvailable ? string.Empty : " (none in the video)")}, in ad {AdDetector.InAd}, drawn by {VideoView.Renderer}");
                }

                return;
            }

            if (_stallReportedAt == 0 && _clockMovedAt != 0 && Stopwatch.GetElapsedTime(_clockMovedAt, now) >= ClockStallThreshold)
            {
                _stallReportedAt = now;

                TvCore.LogError($"[Watchdog] Clock has not moved on from {Time(_lastClock)} for {Stopwatch.GetElapsedTime(_clockMovedAt, now).TotalSeconds:0.0}s, player {_player.State}; {_player.Stats}, in ad {AdDetector.InAd}, window {(IsVisible ? "shown" : "hidden")}");
            }
        }

        private static void LogMenuClicks(ItemsControl menu, string path)
        {
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                var header = $"{path}{item.Header}";

                item.Click += (sender, args) => TvCore.LogInfo($"[UI] Menu: {header}");

                LogMenuClicks(item, header + " > ");
            }
        }
    }
}
