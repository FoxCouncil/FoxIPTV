// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using FoxIPTV.Classes;
    using FoxIPTV.Playback;

    [Collection("AdDetector")]
    public class AdDetectorTests
    {
        private static SpliceSignal Ad(int type, double seconds, int number, int expected)
        {
            var signal = new SpliceSignal { Command = 6 };

            signal.Segments.Add(new Segmentation { Type = type, Seconds = seconds, Number = number, Expected = expected });

            return signal;
        }

        [Fact]
        public void Signals_CountTheAdsAndEndOnTheLast()
        {
            AdDetector.Reset();

            AdDetector.ObserveSignal(Ad(0x30, 15, 1, 3));

            Assert.True(AdDetector.InAd);
            Assert.Equal(1, AdDetector.AdNumber);
            Assert.Equal(3, AdDetector.AdTotal);
            Assert.InRange(AdDetector.AdSecondsLeft.Value, 14, 15);

            AdDetector.ObserveSignal(Ad(0x31, 0, 1, 3));
            AdDetector.ObserveSignal(Ad(0x30, 30, 2, 3));

            Assert.Equal(2, AdDetector.AdNumber);
            Assert.InRange(AdDetector.AdSecondsLeft.Value, 29, 30);

            AdDetector.ObserveSegment("https://cdn.example/show/episode/seg40.ts");

            Assert.True(AdDetector.InAd);

            AdDetector.ObserveSignal(Ad(0x30, 15, 3, 3));
            AdDetector.ObserveSignal(Ad(0x31, 0, 3, 3));

            Assert.False(AdDetector.InAd);
            Assert.Equal(0, AdDetector.AdTotal);
            Assert.Null(AdDetector.AdSecondsLeft);
        }

        [Fact]
        public void PlutoProgress_CountsDownTheCurrentAd()
        {
            AdDetector.Reset();

            AdDetector.ObserveAdProgress(new AdProgress { Creative = "6abec2e1ac612a761b08e646", Elapsed = 0, Length = 30 });

            Assert.Null(AdDetector.AdSecondsLeft);

            AdDetector.ObserveSegment("https://cdn.example/_ad/creative/6abec2e1ac612a761b08e646_ad/720p/seg1.ts");
            AdDetector.ObserveAdProgress(new AdProgress { Creative = "6abec2e1ac612a761b08e646", Elapsed = 10, Length = 30 });

            Assert.InRange(AdDetector.AdSecondsLeft.Value, 19, 20);

            AdDetector.ObserveSegment("https://cdn.example/show/episode/seg40.ts");

            Assert.Null(AdDetector.AdSecondsLeft);
        }

        [Fact]
        public void Signals_SpliceOutCountsDownTheBreakAndSpliceInEndsIt()
        {
            AdDetector.Reset();

            AdDetector.ObserveSignal(new SpliceSignal { Command = 5, OutOfNetwork = true, BreakSeconds = 60 });

            Assert.True(AdDetector.InAd);
            Assert.Equal(60, AdDetector.SecondsLeft.Value, 1);

            AdDetector.ObserveSignal(new SpliceSignal { Command = 5, InToNetwork = true });

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void Roku_SignalledBreakCountsAdsByAddressAndEndsOnProgramme()
        {
            AdDetector.Reset();

            AdDetector.ObserveSegment("https://live1250.delivery.roku.com/0c24a9ef/t2-origin/out/v1/live_1080/00188/live_1080_07150.ts", duration: 2);
            AdDetector.ObserveSegment("https://osm-ads.delivery.roku.com/TRC_FightInc_15_16x9.mp4_v2_1_re/hls/transcode_segment_0_0000.ts", duration: 2);
            AdDetector.ObserveDiscontinuity();
            AdDetector.ObserveSignal(new SpliceSignal { Command = 5, OutOfNetwork = true, BreakSeconds = 150 });

            Assert.True(AdDetector.InAd);
            Assert.Equal(1, AdDetector.AdNumber);
            Assert.Equal(150, AdDetector.SecondsLeft.Value, 1);

            AdDetector.ObserveSegment("https://osm-ads.delivery.roku.com/TRC_FightInc_15_16x9.mp4_v2_1_re/hls/transcode_segment_0_0001.ts", duration: 2);
            AdDetector.ObserveSegment("https://osm-ads.delivery.roku.com/d8ddc091-7b60-4b05-a7d1-834891db9047/hls/transcode_segment_0_0000.ts", duration: 2);
            AdDetector.ObserveDiscontinuity();

            Assert.Equal(2, AdDetector.AdNumber);

            AdDetector.ObserveSegment("https://osm-ads.delivery.roku.com/c7ec0a30-6cd9-4f81-ba72-2f662dd82d67/hls/transcode_segment_0_0000.ts", duration: 2);
            AdDetector.ObserveDiscontinuity();

            Assert.Equal(3, AdDetector.AdNumber);

            AdDetector.ObserveSegment("https://live1250.delivery.roku.com/0c24a9ef/t2-origin/out/v1/live_1080/00188/live_1080_07227.ts", duration: 2);
            AdDetector.ObserveDiscontinuity();

            Assert.False(AdDetector.InAd);
        }

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
            Assert.Equal(60, AdDetector.SecondsLeft.Value, 1);

            AdDetector.ObserveSegment("https://amagi.example/ad/cue-out-60.000000/b.ts?media_type=A&dur=6.0");

            Assert.Equal(54, AdDetector.SecondsLeft.Value, 1);
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

        private const string SamsungHost = "https://pb-efxa6c2xgfuer.akamaized.net/live-content";

        private static void Piece(string path, string title = null, bool discontinuity = false)
        {
            AdDetector.ObserveSegment($"{SamsungHost}/{path}", title);

            if (discontinuity)
            {
                AdDetector.ObserveDiscontinuity();
            }
        }

        [Fact]
        public void Samsung_StayTunedSlateBetweenBumpersIsOneBreak()
        {
            AdDetector.Reset();

            Piece("260/content/XM0BMWGHHNFFHF/22254038/6_353.ts", "pid=260");

            Assert.False(AdDetector.InAd);

            Piece("260/content/XM0BMWGHHNFFHF/22254038/split_202607220259/6_354_a.ts", "pid=260");

            Assert.False(AdDetector.InAd);

            Piece("149/modified_bumpers/149/content/XM0F3CI209SFPU/24347824/6_000.ts", null, true);

            Assert.True(AdDetector.InAd);

            Piece("149/content/XM0GRQO3JXT502/27129464/6_000.ts", null, true);
            Piece("149/content/XM0GRQO3JXT502/27129464/6_001.ts");
            Piece("149/content/XM0GRQO3JXT502/27129464/6_023.ts");
            Piece("149/modified_bumpers/149/content/XM0KRTN9K12S8L/24347865/6_000.ts", null, true);

            Assert.True(AdDetector.InAd);
            Assert.Equal(1, AdDetector.AdNumber);

            Piece("260/content/XM0BMWGHHNFFHF/22254038/split_202607220259/6_354_b.ts", "pid=260", true);

            Assert.False(AdDetector.InAd);

            Piece("260/content/XM0BMWGHHNFFHF/22254038/6_355.ts", "pid=260");

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void Samsung_BreakEndsAtTheNextProgrammePieceWhenTheSplitEndIsMissing()
        {
            AdDetector.Reset();

            Piece("260/content/XM0BMWGHHNFFHF/22254038/split_202607220259/6_354_a.ts", "pid=260");
            Piece("149/content/XM0GRQO3JXT502/27129464/6_000.ts", null, true);

            Assert.True(AdDetector.InAd);

            Piece("260/content/XM0BMWGHHNFFHF/22254038/6_355.ts", "pid=260", true);

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void Samsung_BumperStartsABreakWithoutASplitPiece()
        {
            AdDetector.Reset();

            Piece("260/content/XM0BMWGHHNFFHF/22254038/6_353.ts", "pid=260");
            Piece("149/modified_bumpers/149/content/XM0F3CI209SFPU/24347824/6_000.ts", null, true);
            Piece("149/content/XM0GRQO3JXT502/27129464/6_000.ts", null, true);

            Assert.True(AdDetector.InAd);

            Piece("260/content/XM0BMWGHHNFFHF/22254038/6_354.ts", "pid=260", true);

            Assert.False(AdDetector.InAd);
        }

        private const string RokuPiece = "https://aka-live1050.delivery.roku.com/c8620cf9-c802-4867-a846-ed2960692499/t2-origin/out/v1/live_1_low/live_1_low_{0}.ts";

        private static void Tagged(string url, double duration, params string[] marks)
        {
            AdDetector.ObserveSegment(url, null, marks, duration);
        }

        [Fact]
        public void Roku_CueTagsHoldTheBreakAndCountDown()
        {
            AdDetector.Reset();

            Tagged(string.Format(RokuPiece, 2216970), 4.0);

            Assert.False(AdDetector.InAd);

            Tagged(string.Format(RokuPiece, 2216971), 2.2, "#EXT-OATCLS-SCTE35:/DCaAAAAAAAAAP/wFAUAAZSnf+//PA9Fqf4ApMt/", "#EXT-X-ASSET:CAID=0x6C6177616E646372696D655F6C696E656172", "#EXT-X-CUE-OUT:120.000");

            Assert.True(AdDetector.InAd);
            Assert.Equal(120, AdDetector.SecondsLeft.Value, 1);

            Tagged(string.Format(RokuPiece, 2216972), 4.0, "#EXT-X-CUE-OUT-CONT:CAID=0x6C6177616E646372696D655F6C696E656172,ElapsedTime=2.200,Duration=120.000,SCTE35=/DCzAAAAAAAAAACwBQb/PA9FqQCdAiZDVUVJ");

            Assert.True(AdDetector.InAd);
            Assert.Equal(117.8, AdDetector.SecondsLeft.Value, 1);

            Tagged(string.Format(RokuPiece, 2217001), 1.8, "#EXT-X-CUE-OUT-CONT:CAID=0x6C6177616E646372696D655F6C696E656172,ElapsedTime=118.200,Duration=120.000,SCTE35=/DCzAAAAAAAAAACwBQb/PA9FqQCdAiZDVUVJ");

            Assert.True(AdDetector.InAd);
            Assert.Equal(1.8, AdDetector.SecondsLeft.Value, 1);
            Assert.Equal(1, AdDetector.AdNumber);

            Tagged(string.Format(RokuPiece, 2217002), 2.2, "#EXT-OATCLS-SCTE35:/DAgAAAAAAAAAP/wDwUAAZSnf0//PLQRKQAAAAAAADZm0SY=", "#EXT-X-CUE-IN");

            Assert.False(AdDetector.InAd);
            Assert.Null(AdDetector.SecondsLeft);
        }

        [Fact]
        public void Wurl_JoiningMidBreakStillShowsTheAd()
        {
            AdDetector.Reset();

            Tagged("https://bec-spin-1-us.plex.wurl.tv/5/hls-v3/2208084-1.ts", 6.006, "#EXT-X-CUE-OUT-CONT:ElapsedTime=30.03,Duration=120,SCTE35=WURL1790497008");

            Assert.True(AdDetector.InAd);
            Assert.Equal(89.97, AdDetector.SecondsLeft.Value, 1);

            Tagged("https://bec-spin-1-us.plex.wurl.tv/5/hls-v3/2208100-1.ts", 6.006, "#EXT-X-CUE-IN");

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void DateRange_StartsTheBreakAndDiscontinuitiesCountTheAds()
        {
            AdDetector.Reset();

            Tagged("https://dai.google.com/linear/pods/v1/seg/ad/1.ts", 4.992, "#EXT-X-DATERANGE:ID=\"123404-1790627197\",START-DATE=\"2026-09-28T20:26:37.663116Z\",PLANNED-DURATION=179.996489,SCTE35-OUT=0xFC304E0000");
            AdDetector.ObserveDiscontinuity();

            Assert.True(AdDetector.InAd);
            Assert.Equal(179.996, AdDetector.SecondsLeft.Value, 1);

            Tagged("https://dai.google.com/linear/pods/v1/seg/ad/2.ts", 4.992);
            AdDetector.ObserveDiscontinuity();
            Tagged("https://dai.google.com/linear/pods/v1/seg/ad/3.ts", 4.992);
            AdDetector.ObserveDiscontinuity();

            Assert.Equal(3, AdDetector.AdNumber);

            Tagged("https://propee33f9c2-s.vtg.paramount.tech/index-english=4011-1.ts", 2.816, "#EXT-X-CUE-IN");
            AdDetector.ObserveDiscontinuity();

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void Amagi_AdStartRunsForItsLengthOnce()
        {
            AdDetector.Reset();

            const string beacon = "https://amg00793-amg00793c6-plex-us-2667.playouts.now.amagi.tv/ts-us-e2-n2/beacon/amg00793-bbcstudios-bbcearthaall-plexus/{0}.ts";
            const string start = "#EXT-X-AD-START:URI=\"https://amg00793-amg00793c6-plex-us-2667.playouts.now.amagi.tv/ts-us-e2-n2/beacon/ad-metadata/amg00793-bbcstudios-bbcearthaall-plexus/cb51391e?break_type=MID_ROLL&dur=120.000000&id=amg00793-bbcstudios-bbcearthaall-plexus_488987-cue-out-120.053000_default&msn=488994&sts=13.146\"";

            Tagged(string.Format(beacon, 488992), 6.673, start);

            Assert.True(AdDetector.InAd);
            Assert.Equal(120, AdDetector.SecondsLeft.Value, 1);

            Tagged(string.Format(beacon, 488993), 6.673, start);

            for (var piece = 488994; piece <= 489010; piece++)
            {
                Tagged(string.Format(beacon, piece), 6.673, piece == 489005 ? new[] { start } : Array.Empty<string>());

                Assert.True(AdDetector.InAd, $"piece {piece}");
            }

            Tagged(string.Format(beacon, 489011), 6.673);

            Assert.False(AdDetector.InAd);

            Tagged(string.Format(beacon, 489012), 6.673, start);

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void Amagi_NewAdStartInsideABreakIsTheNextAd()
        {
            AdDetector.Reset();

            const string beacon = "https://amg00793-amg00793c6-plex-us-2667.playouts.now.amagi.tv/ts-us-e2-n2/beacon/amg00793-bbcstudios-bbcearthaall-plexus/{0}.ts";
            const string midRoll = "#EXT-X-AD-START:URI=\"https://amg00793-amg00793c6-plex-us-2667.playouts.now.amagi.tv/ts-us-e2-n2/beacon/ad-metadata/amg00793-bbcstudios-bbcearthaall-plexus/cb51391e?break_type=MID_ROLL&dur=120.000000&id=amg00793-bbcstudios-bbcearthaall-plexus_488987-cue-out-120.053000_default&msn=488994&sts=13.146\"";
            const string lBand = "#EXT-X-AD-START:URI=\"https://amg00793-amg00793c6-plex-us-2667.playouts.now.amagi.tv/ts-us-e2-n2/beacon/ad-metadata/amg00793-bbcstudios-bbcearthaall-plexus/cb51391e?break_type=LBAND&dur=15.000000&id=amg00793-bbcstudios-bbcearthaall-plexus_hard-489000-lband-15.000000_default&msn=489007&sts=13.347\"";

            Tagged(string.Format(beacon, 488992), 6.673, midRoll);

            for (var piece = 488993; piece <= 489004; piece++)
            {
                Tagged(string.Format(beacon, piece), 6.673);
            }

            Tagged(string.Format(beacon, 489005), 6.673, lBand);

            Assert.True(AdDetector.InAd);
            Assert.Equal(2, AdDetector.AdNumber);
            Assert.Equal(15, AdDetector.SecondsLeft.Value, 1);

            Tagged(string.Format(beacon, 489006), 6.673);
            Tagged(string.Format(beacon, 489007), 6.673);

            Assert.True(AdDetector.InAd);

            Tagged(string.Format(beacon, 489008), 6.673);

            Assert.False(AdDetector.InAd);

            Tagged(string.Format(beacon, 489009), 6.673, midRoll);

            Assert.False(AdDetector.InAd);
        }

        [Fact]
        public void CueInAndCueOutOnOnePiece_StartsTheNextBreak()
        {
            AdDetector.Reset();

            Tagged(string.Format(RokuPiece, 1), 4.0, "#EXT-X-CUE-OUT:30");
            Tagged(string.Format(RokuPiece, 2), 4.0, "#EXT-X-CUE-IN", "#EXT-X-CUE-OUT:DURATION=60");

            Assert.True(AdDetector.InAd);
            Assert.Equal(60, AdDetector.SecondsLeft.Value, 1);
        }
    }
}
