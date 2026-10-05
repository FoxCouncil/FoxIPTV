// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(FoxIPTV.Tests.HeadlessApp))]

namespace FoxIPTV.Tests
{
    using System;
    using System.Reflection;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Primitives;
    using Avalonia.Headless;
    using Avalonia.Headless.XUnit;
    using Avalonia.Input;
    using Avalonia.Themes.Fluent;
    using Avalonia.Threading;
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
