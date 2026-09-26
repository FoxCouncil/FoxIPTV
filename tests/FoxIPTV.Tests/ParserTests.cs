// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using FoxIPTV.Classes;

    public class ParserTests
    {
        [Fact]
        public void M3U_ReadsHeaderGuidesAttributesAndOptions()
        {
            const string text = "#EXTM3U url-tvg=\"http://a/guide.xml, http://b/guide.xml.gz\"\n" +
                                "#EXTINF:-1 tvg-id=\"one.us\" tvg-logo=\"http://a/1.png\" group-title=\"News, Weather\" tvg-chno=\"7\",Channel One\n" +
                                "#EXTVLCOPT:http-user-agent=Test Agent\n" +
                                "http://a/one.m3u8\n" +
                                "#EXTINF:-1,Channel Two\n" +
                                "#EXTGRP:Sports\n" +
                                "http://a/two.m3u8\n";

            var playlist = M3UParser.Parse(text);

            Assert.Equal(new[] { "http://a/guide.xml", "http://b/guide.xml.gz" }, playlist.GuideUrls);
            Assert.Equal(2, playlist.Entries.Count);

            var one = playlist.Entries[0];

            Assert.Equal(7u, one.Index);
            Assert.Equal("one.us", one.Id);
            Assert.Equal("Channel One", one.Name);
            Assert.Equal("News, Weather", one.Group);
            Assert.Equal("http://a/1.png", one.Logo);
            Assert.Equal("Test Agent", one.Options["http-user-agent"]);

            var two = playlist.Entries[1];

            Assert.Equal(2u, two.Index);
            Assert.Equal("Sports", two.Group);
            Assert.Equal("http://a/two.m3u8", two.Url);
        }

        [Fact]
        public void Xmltv_ReadsProgrammesAndSkipsBrokenTimes()
        {
            const string text = "<?xml version=\"1.0\"?><tv>" +
                                "<programme start=\"20260925180000 +0000\" stop=\"20260925190000 +0000\" channel=\"one.us\"><title>News</title><desc>Tonight</desc></programme>" +
                                "<programme start=\"garbage\" stop=\"20260925190000 +0000\" channel=\"one.us\"><title>Broken</title></programme>" +
                                "<programme start=\"202609251900\" stop=\"202609251830\" channel=\"one.us\"><title>Backwards</title></programme>" +
                                "</tv>";

            var guide = XmltvParser.Parse(text);

            var programme = Assert.Single(guide);

            Assert.Equal("one.us", programme.Channel);
            Assert.Equal("News", programme.Title);
            Assert.Equal("Tonight", programme.Description);
            Assert.Equal(new DateTimeOffset(2026, 9, 25, 18, 0, 0, TimeSpan.Zero), programme.Start);
            Assert.Equal(6, programme.BlockLength);
        }
    }
}
