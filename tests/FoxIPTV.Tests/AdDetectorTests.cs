// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using FoxIPTV.Classes;

    /// <summary>AdDetector keeps static state, so these run one at a time</summary>
    [Collection("AdDetector")]
    public class AdDetectorTests
    {
        [Fact]
        public void Pluto_CountsAdsByCreativeAndEndsOnProgramme()
        {
            AdDetector.Reset();

            AdDetector.Observe("Retrieving https://cdn.example/_ad/creative/0123456789abcdef0123/seg1.ts");

            Assert.True(AdDetector.InAd);
            Assert.Equal(1, AdDetector.AdNumber);

            AdDetector.Observe("Retrieving https://cdn.example/_ad/creative/0123456789abcdef0123/seg2.ts");

            Assert.Equal(1, AdDetector.AdNumber);

            AdDetector.Observe("Retrieving https://cdn.example/_ad/creative/fedcba9876543210fedc/seg1.ts");

            Assert.Equal(2, AdDetector.AdNumber);

            AdDetector.Observe("Retrieving https://cdn.example/show/episode/seg40.ts");

            Assert.False(AdDetector.InAd);
            Assert.Equal(0, AdDetector.AdNumber);
        }

        [Fact]
        public void Playlists_AndSubtitles_AreIgnored()
        {
            AdDetector.Reset();

            AdDetector.Observe("Retrieving https://cdn.example/_ad/creative/0123456789abcdef0123/index.m3u8");
            AdDetector.Observe("Retrieving https://cdn.example/_ad/creative/0123456789abcdef0123/subs.vtt");

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void Amagi_CountsDownTheBreak()
        {
            AdDetector.Reset();

            AdDetector.Observe("Retrieving https://amagi.example/ad/cue-out-60.000000/a.ts?media_type=A&dur=6.0");

            Assert.True(AdDetector.InAd);
            Assert.Equal(60, AdDetector.SecondsLeft);

            AdDetector.Observe("Retrieving https://amagi.example/ad/cue-out-60.000000/b.ts?media_type=A&dur=6.0");

            Assert.Equal(54, AdDetector.SecondsLeft);
        }

        [Fact]
        public void Discontinuities_CountAdsWhereNamesDoNot()
        {
            AdDetector.Reset();

            AdDetector.Observe("Retrieving https://stitcher.example/v1/segment/abc/1.ts");
            AdDetector.Observe("Restarting demuxer 0 1");
            AdDetector.Observe("Restarting demuxer 0 1");

            Assert.Equal(2, AdDetector.AdNumber);
        }
    }
}
