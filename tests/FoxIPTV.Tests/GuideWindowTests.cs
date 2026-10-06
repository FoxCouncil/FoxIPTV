// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(FoxIPTV.Tests.HeadlessApp))]

namespace FoxIPTV.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Primitives;
    using Avalonia.Headless;
    using Avalonia.Headless.XUnit;
    using Avalonia.Input;
    using Avalonia.Themes.Fluent;
    using Avalonia.Threading;
    using FoxIPTV.Classes;
    using FoxIPTV.Views;

    public class HeadlessApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
        }

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<HeadlessApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    public class GuideWindowTests
    {
        private static DateTimeOffset? ViewStart(GuideView guide)
        {
            return (DateTimeOffset?)typeof(GuideView).GetField("_startUtc", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(guide);
        }

        private static void Click(Window window, Control control)
        {
            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window).Value;

            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }

        private static void SetCore(string name, object value)
        {
            typeof(TvCore).GetProperty(name, BindingFlags.Public | BindingFlags.Static).SetValue(null, value);
        }

        private static void LoadChannels()
        {
            var now = DateTimeOffset.UtcNow;
            var channels = new List<Channel>
            {
                new Channel { Index = 1, Id = "a", Name = "Alpha News", Group = "News" },
                new Channel { Index = 2, Id = "b", Name = "Bravo Movies", Group = "Movies" },
                new Channel { Index = 3, Id = "c", Name = "Charlie News", Group = "News" }
            };
            var guide = channels.Select(x => new Programme { Channel = x.Id, Title = $"{x.Name} Show", Description = "About it", Start = now.AddMinutes(-20), Stop = now.AddMinutes(40) }).ToList();

            SetCore(nameof(TvCore.Channels), channels);
            SetCore(nameof(TvCore.ChannelIndexList), channels.Select(x => x.Index).ToList());
            SetCore(nameof(TvCore.Guide), guide);
            SetCore(nameof(TvCore.State), TvCoreState.Running);
            SetCore(nameof(TvCore.CurrentChannel), null);
            SetCore(nameof(TvCore.CurrentChannelIndex), 0u);
            TvCore.ChannelFavorites.Clear();
        }

        private static void ClearChannels()
        {
            SetCore(nameof(TvCore.Channels), null);
            SetCore(nameof(TvCore.ChannelIndexList), null);
            SetCore(nameof(TvCore.Guide), null);
            SetCore(nameof(TvCore.State), TvCoreState.None);
            SetCore(nameof(TvCore.CurrentChannel), null);
            SetCore(nameof(TvCore.CurrentChannelIndex), 0u);
            TvCore.ChannelFavorites.Clear();
        }

        private static (GuideWindow Window, GuideView Guide) OpenGuide()
        {
            var window = new GuideWindow();

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var guide = window.FindControl<GuideView>("Guide");

            guide.ResetView();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            return (window, guide);
        }

        private static Point InGuide(Window window, GuideView guide, double x, int row)
        {
            return guide.TranslatePoint(new Point(x, 36 + row * 56 + 28), window).Value;
        }

        [AvaloniaFact]
        public void ClickingAShow_ShowsItsDetailsWithoutSwitching()
        {
            LoadChannels();

            try
            {
                var (window, guide) = OpenGuide();
                var point = InGuide(window, guide, 400, 1);

                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("Bravo Movies Show", window.FindControl<TextBlock>("ProgrammeTitleLabel").Text);
                Assert.Equal("2 Bravo Movies", window.FindControl<TextBlock>("ChannelNameLabel").Text);
                Assert.Null(TvCore.CurrentChannel);
            }
            finally
            {
                ClearChannels();
            }
        }

        [AvaloniaFact]
        public void DoubleClickingAShow_SwitchesToItsChannel()
        {
            LoadChannels();

            try
            {
                var (window, guide) = OpenGuide();
                var point = InGuide(window, guide, 400, 2);

                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("c", TvCore.CurrentChannel?.Id);
            }
            finally
            {
                ClearChannels();
            }
        }

        [AvaloniaFact]
        public void ClickingAChannel_SwitchesToIt()
        {
            LoadChannels();

            try
            {
                var (window, guide) = OpenGuide();
                var point = InGuide(window, guide, 120, 1);

                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("b", TvCore.CurrentChannel?.Id);
                Assert.Equal("Bravo Movies Show", window.FindControl<TextBlock>("ProgrammeTitleLabel").Text);
            }
            finally
            {
                ClearChannels();
            }
        }

        [AvaloniaFact]
        public void Filter_ShowsOnlyThatGroupOrTheFavourites()
        {
            LoadChannels();

            try
            {
                TvCore.ChannelFavorites.Add("b");

                var (window, guide) = OpenGuide();
                var combo = window.FindControl<ComboBox>("FilterCombo");
                var labels = combo.ItemsSource.Cast<string>().ToList();

                Assert.Equal(new[] { "All Channels", "Favorite Channels", "Movies", "News" }, labels);
                Assert.Equal(3, guide.ShownCount);

                combo.SelectedIndex = labels.IndexOf("News");

                Assert.Equal(2, guide.ShownCount);
                Assert.Equal("2 stations", window.FindControl<TextBlock>("StationCountLabel").Text);

                combo.SelectedIndex = labels.IndexOf("Favorite Channels");

                Assert.Equal(1, guide.ShownCount);
            }
            finally
            {
                ClearChannels();
            }
        }

        [AvaloniaFact]
        public void ArrowKeys_MoveTheTimeAfterResetView()
        {
            var window = new GuideWindow();

            window.Show();

            var guide = window.FindControl<GuideView>("Guide");

            Click(window, window.FindControl<Button>("ResetViewButton"));

            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);

            Assert.NotNull(ViewStart(guide));

            window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);

            Assert.Null(ViewStart(guide));
        }

        [AvaloniaFact]
        public void TimeBar_FollowsTheKeysAndMovesTheGuide()
        {
            var window = new GuideWindow();

            window.Show();

            var guide = window.FindControl<GuideView>("Guide");
            var bar = window.FindControl<ScrollBar>("GuideTimeBar");

            Dispatcher.UIThread.RunJobs();

            Assert.True(bar.IsVisible && bar.Bounds.Height > 0, "the time bar is not shown");
            Assert.Equal(4, bar.Value);

            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(5, bar.Value);

            var start = ViewStart(guide);
            var track = bar.TranslatePoint(new Point(bar.Bounds.Width - 30, bar.Bounds.Height / 2), window).Value;

            window.MouseDown(track, MouseButton.Left);
            window.MouseUp(track, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.True(ViewStart(guide) > start, $"clicking the bar left the guide at {ViewStart(guide)}");
        }

        [AvaloniaFact]
        public void ArrowKeys_StayInTheSearchBox()
        {
            var window = new GuideWindow();

            window.Show();

            var guide = window.FindControl<GuideView>("Guide");

            Click(window, window.FindControl<TextBox>("SearchBox"));

            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);

            Assert.Null(ViewStart(guide));
        }
    }
}
