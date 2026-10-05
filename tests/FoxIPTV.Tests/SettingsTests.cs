// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.Linq;
    using System.Text;
    using Avalonia.Controls;
    using Avalonia.Headless.XUnit;
    using FoxIPTV.Classes;
    using FoxIPTV.Playback.Hls;
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
        public void AdTitles_LoadWithoutDoubling()
        {
            var saved = JsonConvert.SerializeObject(new Settings());
            var settings = new Settings();

            JsonConvert.PopulateObject(saved, settings);
            JsonConvert.PopulateObject(saved, settings);

            Assert.Equal(new Settings().AdTitles, settings.AdTitles);
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
