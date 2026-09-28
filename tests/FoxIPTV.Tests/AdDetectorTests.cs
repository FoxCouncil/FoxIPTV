// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using FoxIPTV.Classes;

    [Collection("AdDetector")]
    public class AdDetectorTests
    {
        [Fact]
        public void Pluto_CountsAdsByCreativeAndEndsOnProgramme()
        {
            AdDetector.Reset();

            AdDetector.ObserveSegment("https://cdn.example/_ad/creative/0123456789abcdef0123/seg1.ts");

            Assert.True(AdDetector.InAd);
            Assert.Equal(1, AdDetector.AdNumber);

            AdDetector.ObserveSegment("https://cdn.example/_ad/creative/0123456789abcdef0123/seg2.ts");

            Assert.Equal(1, AdDetector.AdNumber);

            AdDetector.ObserveSegment("https://cdn.example/_ad/creative/fedcba9876543210fedc/seg1.ts");

            Assert.Equal(2, AdDetector.AdNumber);

            AdDetector.ObserveSegment("https://cdn.example/show/episode/seg40.ts");

            Assert.False(AdDetector.InAd);
            Assert.Equal(0, AdDetector.AdNumber);
        }

        [Fact]
        public void Playlists_AndSubtitles_AreIgnored()
        {
            AdDetector.Reset();

            AdDetector.ObserveSegment("https://cdn.example/_ad/creative/0123456789abcdef0123/index.m3u8");
            AdDetector.ObserveSegment("https://cdn.example/_ad/creative/0123456789abcdef0123/subs.vtt");

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void Amagi_CountsDownTheBreak()
        {
            AdDetector.Reset();

            AdDetector.ObserveSegment("https://amagi.example/ad/cue-out-60.000000/a.ts?media_type=A&dur=6.0");

            Assert.True(AdDetector.InAd);
            Assert.Equal(60, AdDetector.SecondsLeft);

            AdDetector.ObserveSegment("https://amagi.example/ad/cue-out-60.000000/b.ts?media_type=A&dur=6.0");

            Assert.Equal(54, AdDetector.SecondsLeft);
        }

        [Fact]
        public void Samsung_AdRedirectTarget_StaysInTheBreak()
        {
            AdDetector.Reset();

            AdDetector.ObserveSegment("https://pb-fkohs8pswgk6n.akamaized.net/v1/segment/3722c60a815c199d9c0ef36c5b73da68a62b09d1/pb-fkohs8pswgk6n/8c3d1d19/3/4701254");
            AdDetector.ObserveSegment("https://unified-ad-segment-cdn-ak-us-east-2.akamaized.net/tm/3722c60a815c199d9c0ef36c5b73da68a62b09d1/69ce296b/asset_1080_8_3_00003.ts");

            Assert.True(AdDetector.InAd);

            AdDetector.ObserveSegment("https://pb-fkohs8pswgk6n.akamaized.net/out/v1/abc/HLS_video_5_1234.ts");

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void Discontinuities_CountAdsWhereNamesDoNot()
        {
            AdDetector.Reset();

            AdDetector.ObserveSegment("https://stitcher.example/v1/segment/abc/1.ts");
            AdDetector.ObserveDiscontinuity();
            AdDetector.ObserveDiscontinuity();

            Assert.Equal(2, AdDetector.AdNumber);
        }
    }
}
