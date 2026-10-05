// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.Linq;
    using FoxIPTV.Playback;
    using FoxIPTV.Playback.Hls;

    public class SubtitleTests
    {
        private static byte Odd(int value)
        {
            var bits = 0;

            for (var i = 0; i < 7; i++)
            {
                bits += (value >> i) & 1;
            }

            return (byte)(bits % 2 == 0 ? value | 0x80 : value);
        }

        private static unsafe string Feed(CaptionDecoder decoder, (int Hi, int Lo)[] pairs, int field)
        {
            string text = null;
            var pts = 0L;

            foreach (var (hi, lo) in pairs)
            {
                var data = new byte[] { (byte)(field == 0 ? 0xFC : 0xFD), Odd(hi), Odd(lo), (byte)(field == 0 ? 0xFD : 0xFC), Odd(0), Odd(0) };

                fixed (byte* bytes = data)
                {
                    text = decoder.Decode(bytes, data.Length, pts) ?? text;
                }

                pts += 300000;
            }

            return text;
        }

        [Fact]
        public void Cc3_IsReadFromTheSecondField()
        {
            Assert.True(FFmpegNative.Initialize(), FFmpegNative.Failure);

            var hola = new[] { (0x15, 0x25), ('H', 'O'), ('L', 'A'), (0x00, 0x00) };

            using var first = CaptionDecoder.Open(0);
            using var second = CaptionDecoder.Open(1);

            Assert.Null(Feed(first, hola, 1));
            Assert.Equal("HOLA", Feed(second, hola, 1)?.Trim());
        }

        [Theory]
        [InlineData("es-US", null, "spa", true)]
        [InlineData("en", null, "eng", true)]
        [InlineData("es", null, "en", false)]
        [InlineData(null, "cc3", "cc3", true)]
        [InlineData(null, "cc1", "cc3", false)]
        [InlineData("en", null, null, false)]
        public void CaptionTrack_MatchesThePreferredLanguage(string language, string id, string preferred, bool matches)
        {
            Assert.Equal(matches, new CaptionTrack { Id = id ?? "vtt:x", Language = language }.Matches(preferred));
        }

        private static readonly Uri Base = new Uri("https://cdn.example/live/master.m3u8");

        [Theory]
        [InlineData("SUBTITLES=\"subs\"", "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"English\",DEFAULT=NO,FORCED=NO,URI=\"subtitle/subs/en/playlist.m3u8\"", "https://cdn.example/live/subtitle/subs/en/playlist.m3u8")]
        [InlineData("SUBTITLES=\"subs\"", "#EXT-X-MEDIA:LANGUAGE=\"eng\",AUTOSELECT=YES,TYPE=SUBTITLES,URI=\"en/3.m3u8\",GROUP-ID=\"subs\",DEFAULT=YES,NAME=\"English-VTT\"", "https://cdn.example/live/en/3.m3u8")]
        [InlineData("SUBTITLES=\"subs\",CLOSED-CAPTIONS=NONE", "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"English\",LANGUAGE=\"en\",URI=\"en.m3u8\"", "https://cdn.example/live/en.m3u8")]
        [InlineData("SUBTITLES=\"subs\",CLOSED-CAPTIONS=\"cc\"", "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"English\",LANGUAGE=\"en\",URI=\"en.m3u8\"", null)]
        [InlineData("SUBTITLES=\"subs\"", "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"Forced\",LANGUAGE=\"en\",FORCED=YES,URI=\"forced.m3u8\"", null)]
        [InlineData("SUBTITLES=\"subs\",CODECS=\"avc1.64001f,mp4a.40.2,stpp.ttml.im1t\"", "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"English\",LANGUAGE=\"en\",URI=\"en.m3u8\"", null)]
        [InlineData("AUDIO=\"aud\"", "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"English\",LANGUAGE=\"en\",URI=\"en.m3u8\"", null)]
        public void Master_PicksTheSubtitleTrack(string variantAttributes, string media, string expected)
        {
            var playlist = HlsPlaylist.Parse($"#EXTM3U\n{media}\n#EXT-X-STREAM-INF:BANDWIDTH=1539795,{variantAttributes}\nvideo.m3u8\n", Base);

            var picked = HlsLoader.Subtitle(playlist, playlist.Variants[0]);

            Assert.Equal(expected, picked?.Uri.ToString());
        }

        [Fact]
        public void Master_PrefersEnglishWhenNothingIsDefault()
        {
            var playlist = HlsPlaylist.Parse("#EXTM3U\n" +
                                             "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"Español\",LANGUAGE=\"es\",AUTOSELECT=YES,URI=\"es.m3u8\"\n" +
                                             "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"English\",LANGUAGE=\"en-US\",URI=\"en.m3u8\"\n" +
                                             "#EXT-X-STREAM-INF:BANDWIDTH=1000,SUBTITLES=\"subs\"\nvideo.m3u8\n", Base);

            Assert.Equal("English", HlsLoader.Subtitle(playlist, playlist.Variants[0]).Name);
            Assert.True(playlist.Renditions.All(x => !x.IsForced));
        }

        [Fact]
        public void WebVtt_ReadsCuesOnTheVideoClock()
        {
            const string text = "﻿WEBVTT\r\n" +
                                "X-TIMESTAMP-MAP=LOCAL:10:13:47.936,MPEGTS:5151633745\r\n" +
                                "\r\n" +
                                "NOTE made by the packager\r\n" +
                                "\r\n" +
                                "STYLE\r\n" +
                                "::cue { color: yellow }\r\n" +
                                "\r\n" +
                                "cue-1\r\n" +
                                "10:13:48.436 --> 10:13:50.936 line:85% align:center\r\n" +
                                "<c.white.bg_black>WHERE ARE YOU</c>\r\n" +
                                "<i>GOING &amp; WHY?</i>\r\n" +
                                "\r\n" +
                                "10:13:51.000 --> 10:13:52.000\r\n" +
                                "<v Narrator>Hello&nbsp;there</v>\r\n";

            var cues = WebVtt.Parse(text);
            var zero = 5151633745 / WebVtt.MpegTsHz;

            Assert.Equal(2, cues.Count);
            Assert.Equal(zero + 0.5, cues[0].Start, 3);
            Assert.Equal(zero + 3.0, cues[0].End, 3);
            Assert.Equal("WHERE ARE YOU\nGOING & WHY?", cues[0].Text);
            Assert.Equal(zero + 3.064, cues[1].Start, 3);
            Assert.Equal("Hello there", cues[1].Text);
        }

        [Fact]
        public void WebVtt_WithoutAMapCountsFromZero()
        {
            var cues = WebVtt.Parse("WEBVTT\n\n01:02.500 --> 01:04.000\nLine\n\n00:00:05.000 --> 00:00:04.000\nBackwards\n\n00:00:06.000 --> 00:00:07.000\n<b></b>\n");

            var cue = Assert.Single(cues);

            Assert.Equal(62.5, cue.Start, 3);
            Assert.Equal(64.0, cue.End, 3);
        }

        [Theory]
        [InlineData("")]
        [InlineData("#EXTM3U\n#EXTINF:6,\nseg.vtt\n")]
        [InlineData("1\n00:00:01,000 --> 00:00:02,000\nSRT text\n")]
        public void WebVtt_RefusesAnythingElse(string text)
        {
            Assert.Null(WebVtt.Parse(text));
        }

        [Fact]
        public void Track_ShowsCuesOnTheirVideoRun()
        {
            var track = new SubtitleTrack();

            track.NoteVideo(3, 57240.0, 10.0);
            track.NoteVideo(3, 57246.0, 16.0);
            track.Add(3, new[] { new WebVttCue { Start = 57241.5, End = 57243.0, Text = "Hello" } });

            Assert.Equal(string.Empty, track.TextAt(11.4));
            Assert.Equal("Hello", track.TextAt(11.6));
            Assert.Equal(string.Empty, track.TextAt(13.1));
        }

        [Fact]
        public void Track_WaitsForTheVideoToArrive()
        {
            var track = new SubtitleTrack();

            track.NoteVideo(0, 100.0, 0.0);
            track.Add(0, new[] { new WebVttCue { Start = 110.0, End = 112.0, Text = "Later" } });

            Assert.Equal(string.Empty, track.TextAt(10.5));

            track.NoteVideo(0, 109.0, 9.0);

            Assert.Equal("Later", track.TextAt(10.5));
        }

        [Fact]
        public void Track_FollowsTheTimestampWrap()
        {
            var track = new SubtitleTrack();
            var unwrapped = SubtitleTrack.WrapSeconds + 20.0;

            track.NoteVideo(7, unwrapped - 30.0, 500.0);
            track.NoteVideo(7, unwrapped + 5.0, 535.0);
            track.Add(7, new[] { new WebVttCue { Start = 21.0, End = 23.0, Text = "Past the wrap" } });

            Assert.Equal("Past the wrap", track.TextAt(531.5));
            Assert.Equal(string.Empty, track.TextAt(530.5));
        }

        [Fact]
        public void Track_JoinsOverlapsAndDropsRepeats()
        {
            var track = new SubtitleTrack();

            track.NoteVideo(1, 0.0, 0.0);
            track.NoteVideo(1, 20.0, 20.0);
            track.Add(1, new[] { new WebVttCue { Start = 2.0, End = 8.0, Text = "Top" }, new WebVttCue { Start = 4.0, End = 6.0, Text = "Bottom" } });
            track.Add(1, new[] { new WebVttCue { Start = 2.0, End = 8.0, Text = "Top" } });

            Assert.Equal("Top", track.TextAt(3.0));
            Assert.Equal("Top\nBottom", track.TextAt(5.0));
            Assert.Equal("Top", track.TextAt(7.0));
        }

        [Fact]
        public void Track_DropsCuesFromABreakTheVideoHasLeft()
        {
            var track = new SubtitleTrack();

            track.NoteVideo(5, 80000.0, 30.0);
            track.NoteVideo(5, 80010.0, 40.0);
            track.Add(4, new[] { new WebVttCue { Start = 80002.0, End = 80004.0, Text = "Gone" } });

            Assert.Equal(string.Empty, track.TextAt(33.0));

            track.NoteVideo(4, 80002.0, 32.0);

            Assert.Equal(string.Empty, track.TextAt(33.0));
        }
    }
}
