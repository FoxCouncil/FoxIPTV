// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.Linq;
    using System.Text;
    using Avalonia.Controls;
    using Avalonia.Headless.XUnit;
    using Avalonia.Markup.Xaml.Styling;
    using Avalonia.Threading;
    using Avalonia.VisualTree;
    using FoxIPTV.Classes;
    using FoxIPTV.Playback.Hls;
    using FoxIPTV.Playback.Video;
    using FoxIPTV.Views;
    using Newtonsoft.Json;

    public class SettingsTests
    {
        private static HlsPlaylist LivePlaylist(int pieces, double seconds)
        {
            var text = new StringBuilder("#EXTM3U\n#EXT-X-TARGETDURATION:2\n#EXT-X-MEDIA-SEQUENCE:100\n");

            for (var i = 0; i < pieces; i++)
            {
                text.Append($"#EXTINF:{seconds:0.0},\npiece{i}.ts\n");
            }

            return HlsPlaylist.Parse(text.ToString(), new Uri("https://cdn.example/live/index.m3u8"));
        }

        [Fact]
        public void LiveDelay_StartsThatFarBehindTheNewestPiece()
        {
            var playlist = LivePlaylist(20, 2);

            Assert.Equal(115, HlsLoader.StartSequence(playlist, 10));
            Assert.Equal(117, HlsLoader.StartSequence(playlist, 0));
            Assert.Equal(100, HlsLoader.StartSequence(playlist, 600));
        }

        [Fact]
        public void AdMessages_LoadWithoutDoubling()
        {
            var saved = JsonConvert.SerializeObject(new Settings());
            var settings = new Settings();

            JsonConvert.PopulateObject(saved, settings);
            JsonConvert.PopulateObject(saved, settings);

            Assert.Equal(new Settings().AdMessages, settings.AdMessages);
        }

        [AvaloniaFact]
        public void SettingsWindow_HasItsThreeTabs()
        {
            var window = new SettingsWindow();
            var tabs = window.FindControl<TabControl>("Tabs");

            Assert.Equal(3, tabs.Items.Count);
            Assert.Equal(10, window.FindControl<ComboBox>("TransparencyCombo").ItemCount);
            Assert.Equal(5, window.FindControl<ComboBox>("LiveDelayCombo").ItemCount);
        }

        [AvaloniaFact]
        public void MenuIcons_ShareTheCheckMarkColumn()
        {
            var plain = new MenuItem { Header = "Quit", Icon = new Image { Width = 16, Height = 16 } };
            var check = new MenuItem { Header = "Guide", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true };
            var menu = new Menu { ItemsSource = new[] { new MenuItem { Header = "Top", ItemsSource = new[] { plain, check } } } };
            var window = new Window { Content = menu, Width = 400, Height = 300 };

            window.Styles.Add(new StyleInclude(new Uri("avares://FoxIPTV/")) { Source = new Uri("avares://FoxIPTV/Views/Styles.axaml") });
            window.Show();

            ((MenuItem)menu.ItemsSource.Cast<object>().First()).IsSubMenuOpen = true;
            Dispatcher.UIThread.RunJobs();

            var icon = plain.GetVisualDescendants().OfType<ContentControl>().Single(x => x.Name == "PART_IconPresenter");
            var tick = check.GetVisualDescendants().OfType<ContentControl>().Single(x => x.Name == "PART_ToggleIconPresenter");

            Assert.Equal(0, Grid.GetColumn(icon));
            Assert.Equal(0, Grid.GetColumn(tick));
            Assert.Equal(icon.Bounds.X, tick.Bounds.X, 1);
        }

        [Fact]
        public void FillSource_CropsTheSidesOrTopToCoverTheFrame()
        {
            Assert.Equal(new Avalonia.Rect(240, 0, 1440, 1080), VideoSurface.FillSource(1920, 1080, 16 / 9.0, 400, 300, true));
            Assert.Equal(new Avalonia.Rect(0, 60, 640, 360), VideoSurface.FillSource(640, 480, 4 / 3.0, 1600, 900, true));
            Assert.Equal(new Avalonia.Rect(0, 0, 640, 480), VideoSurface.FillSource(640, 480, 4 / 3.0, 1600, 900, false));
        }

        [AvaloniaFact]
        public void StatusBarText_IsAllCaps()
        {
            var label = new InkTextBlock { AllCaps = true, Text = "Buffer 87%" };

            Assert.Equal("BUFFER 87%", label.Text);

            label.Text = "Playing";

            Assert.Equal("PLAYING", label.Text);

            label.AllCaps = false;
            label.Text = "Playing";

            Assert.Equal("Playing", label.Text);
        }

        [Theory]
        [InlineData(0, 0, 0xFF3B3B)]
        [InlineData(25, 3, 0xFF3B3B)]
        [InlineData(55, 7, 0xFFC857)]
        [InlineData(100, 12, 0x3DDC84)]
        [InlineData(150, 12, 0x3DDC84)]
        public void BufferMeter_LightsAndColoursByLevel(double percent, int lit, int rgb)
        {
            var color = BufferMeter.LevelColor(percent);

            Assert.Equal(lit, BufferMeter.LitSegments(percent));
            Assert.Equal(rgb, (color.R << 16) | (color.G << 8) | color.B);
        }

        [Theory]
        [InlineData(new[] { 1.5 })]
        [InlineData(new[] { 0.5, 1.78, 1.33 })]
        [InlineData(new[] { 0.4, 0.4, 1.78, 1.5, 0.75, 1.0 })]
        [InlineData(new[] { 3.0, 3.0, 3.0, 0.3 })]
        public void Mosaic_FillsTheScreenEdgeToEdge(double[] aspects)
        {
            var cells = MosaicLayout.Arrange(aspects, 1600, 900);

            Assert.Equal(aspects.Length, cells.Count);
            Assert.Equal(1600 * 900, cells.Sum(x => x.Width * x.Height), 0);

            foreach (var row in cells.GroupBy(x => Math.Round(x.Y, 3)))
            {
                Assert.Equal(1600, row.Sum(x => x.Width), 3);
            }

            Assert.Equal(900, cells.Max(x => x.Bottom), 3);

            for (var i = 0; i < cells.Count; i++)
            {
                for (var j = i + 1; j < cells.Count; j++)
                {
                    Assert.False(cells[i].Intersects(cells[j]) && cells[i].Intersect(cells[j]).Width > 0.001 && cells[i].Intersect(cells[j]).Height > 0.001, $"cells {i} and {j} overlap");
                }
            }
        }

        [Fact]
        public void AdStats_CountBreaksAdsAndTime()
        {
            AdStats.Reset();

            AdStats.Observe("CBC News", true, 1, 0.1);
            AdStats.Observe("CBC News", true, 1, 10);
            AdStats.Observe("CBC News", true, 3, 5);
            AdStats.Observe("CBC News", false, 0, 1);
            AdStats.Observe("Pluto Movies", true, 1, 15);

            var total = AdStats.Total();
            var channel = AdStats.PerChannel().Single(x => x.Channel == "CBC News");

            Assert.Equal(2, total.Breaks);
            Assert.Equal(4, total.Ads);
            Assert.Equal(30.1, total.Seconds, 3);
            Assert.Equal(3, channel.Ads);

            AdStats.Reset();
        }
    }
}
