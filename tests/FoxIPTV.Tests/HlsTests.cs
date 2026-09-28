// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using FFmpeg.AutoGen;
    using FoxIPTV.Playback;
    using FoxIPTV.Playback.Hls;

    public class HlsTests
    {
        private static readonly Uri Base = new Uri("https://cdn.example/live/channel/master.m3u8?token=abc");

        [Fact]
        public void Master_ReadsVariantsAndAudio()
        {
            const string text = "#EXTM3U\n" +
                                "#EXT-X-INDEPENDENT-SEGMENTS\n" +
                                "#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"aud\",NAME=\"English, main\",LANGUAGE=\"en\",DEFAULT=YES,AUTOSELECT=YES,URI=\"audio/en.m3u8\"\n" +
                                "#EXT-X-STREAM-INF:BANDWIDTH=800000,AVERAGE-BANDWIDTH=700000,RESOLUTION=640x360,CODECS=\"avc1.4d401e,mp4a.40.2\",FRAME-RATE=29.970,AUDIO=\"aud\"\n" +
                                "360p/index.m3u8\n" +
                                "#EXT-X-I-FRAME-STREAM-INF:BANDWIDTH=100000,URI=\"iframes.m3u8\"\n" +
                                "#EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080,CODECS=\"avc1.640028,mp4a.40.2\",AUDIO=\"aud\"\n" +
                                "https://other.example/1080p/index.m3u8\n" +
                                "#EXT-X-STREAM-INF:BANDWIDTH=64000,CODECS=\"mp4a.40.5\"\n" +
                                "audio-only.m3u8\n";

            var playlist = HlsPlaylist.Parse(text, Base);

            Assert.True(playlist.IsMaster);
            Assert.Equal(3, playlist.Variants.Count);

            var low = playlist.Variants[0];

            Assert.Equal(800000, low.Bandwidth);
            Assert.Equal(700000, low.AverageBandwidth);
            Assert.Equal(360, low.Height);
            Assert.Equal(29.97, low.FrameRate, 2);
            Assert.Equal("aud", low.AudioGroup);
            Assert.Equal("https://cdn.example/live/channel/360p/index.m3u8", low.Uri.ToString());
            Assert.True(low.HasVideo);

            Assert.Equal("https://other.example/1080p/index.m3u8", playlist.Variants[1].Uri.ToString());
            Assert.False(playlist.Variants[2].HasVideo);

            var audio = Assert.Single(playlist.Renditions);

            Assert.Equal("AUDIO", audio.Type);
            Assert.Equal("English, main", audio.Name);
            Assert.True(audio.IsDefault);
            Assert.Equal("https://cdn.example/live/channel/audio/en.m3u8", audio.Uri.ToString());
        }

        [Fact]
        public void Live_NumbersSegmentsAndCountsDiscontinuities()
        {
            const string text = "#EXTM3U\n" +
                                "#EXT-X-VERSION:3\n" +
                                "#EXT-X-TARGETDURATION:6\n" +
                                "#EXT-X-MEDIA-SEQUENCE:1200\n" +
                                "#EXT-X-DISCONTINUITY-SEQUENCE:40\n" +
                                "#EXT-X-PROGRAM-DATE-TIME:2026-09-27T20:00:00.000Z\n" +
                                "#EXTINF:5.005,\n" +
                                "show/seg1200.ts\n" +
                                "#EXT-X-DISCONTINUITY\n" +
                                "#EXT-X-CUE-OUT:60\n" +
                                "#EXTINF:6.000,\n" +
                                "https://ads.example/_ad/creative/0123456789abcdef0123/seg1.ts\n" +
                                "#EXT-X-CUE-OUT-CONT:6/60\n" +
                                "#EXTINF:6.000,\n" +
                                "https://ads.example/_ad/creative/0123456789abcdef0123/seg2.ts\n" +
                                "#EXT-X-DISCONTINUITY\n" +
                                "#EXT-X-CUE-IN\n" +
                                "#EXTINF:5.005,\n" +
                                "show/seg1203.ts\n";

            var playlist = HlsPlaylist.Parse(text, new Uri("https://cdn.example/live/channel/index.m3u8"));

            Assert.False(playlist.IsMaster);
            Assert.True(playlist.IsLive);
            Assert.Equal(6, playlist.TargetDuration);
            Assert.Equal(4, playlist.Segments.Count);

            Assert.Equal(new long[] { 1200, 1201, 1202, 1203 }, playlist.Segments.Select(x => x.Sequence).ToArray());
            Assert.Equal(new[] { 40, 41, 41, 42 }, playlist.Segments.Select(x => x.DiscontinuitySequence).ToArray());
            Assert.Equal(new[] { false, true, false, true }, playlist.Segments.Select(x => x.Discontinuity).ToArray());

            Assert.Equal(5.005, playlist.Segments[0].Duration, 3);
            Assert.Equal("https://cdn.example/live/channel/show/seg1200.ts", playlist.Segments[0].Uri.ToString());
            Assert.True(Math.Abs((playlist.Segments[1].ProgramDateTime.Value - new DateTimeOffset(2026, 9, 27, 20, 0, 5, 5, TimeSpan.Zero)).TotalMilliseconds) < 1);

            Assert.Equal("#EXT-X-CUE-OUT:60", Assert.Single(playlist.Segments[1].Marks));
            Assert.Equal("#EXT-X-CUE-OUT-CONT:6/60", Assert.Single(playlist.Segments[2].Marks));
            Assert.Equal("#EXT-X-CUE-IN", Assert.Single(playlist.Segments[3].Marks));
            Assert.Empty(playlist.Segments[0].Marks);
        }

        [Fact]
        public void Keys_AesIsPlayableAndSampleAesIsProtected()
        {
            const string text = "#EXTM3U\n" +
                                "#EXT-X-TARGETDURATION:4\n" +
                                "#EXT-X-MEDIA-SEQUENCE:7\n" +
                                "#EXT-X-KEY:METHOD=AES-128,URI=\"https://keys.example/k1\",IV=0x000102030405060708090A0B0C0D0E0F\n" +
                                "#EXTINF:4,\n" +
                                "a.ts\n" +
                                "#EXT-X-KEY:METHOD=NONE\n" +
                                "#EXTINF:4,\n" +
                                "b.ts\n" +
                                "#EXT-X-KEY:METHOD=SAMPLE-AES,URI=\"skd://asset\",KEYFORMAT=\"com.apple.streamingkeydelivery\"\n" +
                                "#EXTINF:4,\n" +
                                "c.ts\n" +
                                "#EXT-X-ENDLIST\n";

            var playlist = HlsPlaylist.Parse(text, Base);

            Assert.False(playlist.IsLive);

            var aes = playlist.Segments[0].Key;

            Assert.True(aes.IsAes128);
            Assert.False(aes.IsCopyProtection);
            Assert.Equal("https://keys.example/k1", aes.Uri.ToString());
            Assert.Equal(Enumerable.Range(0, 16).Select(x => (byte)x).ToArray(), aes.Iv);

            Assert.True(playlist.Segments[1].Key.IsNone);

            var fairPlay = playlist.Segments[2].Key;

            Assert.True(fairPlay.IsCopyProtection);
            Assert.Null(fairPlay.Uri);
        }

        [Fact]
        public void Map_AndByteRanges_CarryOffsets()
        {
            const string text = "#EXTM3U\n" +
                                "#EXT-X-TARGETDURATION:2\n" +
                                "#EXT-X-MAP:URI=\"media.mp4\",BYTERANGE=\"720@0\"\n" +
                                "#EXTINF:2,\n" +
                                "#EXT-X-BYTERANGE:1000@720\n" +
                                "media.mp4\n" +
                                "#EXTINF:2,\n" +
                                "#EXT-X-BYTERANGE:1500\n" +
                                "media.mp4\n" +
                                "#EXT-X-ENDLIST\n";

            var playlist = HlsPlaylist.Parse(text, Base);

            var first = playlist.Segments[0];
            var second = playlist.Segments[1];

            Assert.Equal(720, first.Offset);
            Assert.Equal(1000, first.Length);
            Assert.Equal(1720, second.Offset);
            Assert.Equal(1500, second.Length);

            Assert.NotNull(first.Map);
            Assert.Equal(0, first.Map.Offset);
            Assert.Equal(720, first.Map.Length);
            Assert.Same(first.Map, second.Map);
        }

        [Fact]
        public void Extinf_KeepsThePieceTitle()
        {
            const string text = "#EXTM3U\n" +
                                "#EXT-X-TARGETDURATION:6\n" +
                                "#EXTINF:6.006,pid=260\n" +
                                "show/6_353.ts\n" +
                                "#EXTINF:5.0,\n" +
                                "slate/6_000.ts\n" +
                                "#EXTINF:5.0\n" +
                                "slate/6_001.ts\n";

            var playlist = HlsPlaylist.Parse(text, Base);

            Assert.Equal(new[] { "pid=260", null, null }, playlist.Segments.Select(x => x.Title).ToArray());
            Assert.Equal(6.006, playlist.Segments[0].Duration, 3);
        }

        [Fact]
        public void Attributes_KeepCommasInsideQuotes()
        {
            var attributes = HlsPlaylist.Attributes("TYPE=AUDIO,NAME=\"a, b\",BANDWIDTH=10, CODECS=\"x,y\"");

            Assert.Equal("AUDIO", attributes["TYPE"]);
            Assert.Equal("a, b", attributes["NAME"]);
            Assert.Equal("10", attributes["BANDWIDTH"]);
            Assert.Equal("x,y", attributes["CODECS"]);
        }

        [Fact]
        public void Sniff_KnowsTheContainers()
        {
            var ts = new byte[376];

            ts[0] = 0x47;
            ts[188] = 0x47;

            Assert.Equal("mpegts", HlsLoader.Sniff(ts));

            var mp4 = new byte[] { 0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0, 0, 0, 0 };

            Assert.Equal("mov", HlsLoader.Sniff(mp4));

            var adts = new byte[] { 0xFF, 0xF1, 0x50, 0x80, 0, 0, 0, 0, 0, 0 };

            Assert.Equal("aac", HlsLoader.Sniff(adts));

            var id3 = new byte[] { (byte)'I', (byte)'D', (byte)'3', 4, 0, 0, 0, 0, 0, 2, 0, 0, 0xFF, 0xF1, 0x50, 0x80 };

            Assert.Equal("aac", HlsLoader.Sniff(id3));
        }

        [Fact]
        public unsafe void ChunkReader_StopsAtTheNextPeriod()
        {
            var queue = new ChunkQueue();

            queue.Add(new MediaChunk { Data = new byte[] { 1, 2, 3 }, Period = 0, Duration = 1 }, CancellationToken.None);
            queue.Add(new MediaChunk { Data = new byte[] { 4, 5 }, Period = 0, Duration = 1 }, CancellationToken.None);
            queue.Add(new MediaChunk { Data = new byte[] { 9 }, Period = 1, Duration = 1 }, CancellationToken.None);
            queue.Complete();

            var reader = new ChunkReader(queue, 0, CancellationToken.None);
            var buffer = stackalloc byte[16];

            Assert.Equal(3, reader.Read(buffer, 16));
            Assert.Equal(2, reader.Read(buffer, 16));
            Assert.Equal(4, buffer[0]);
            Assert.Equal(ffmpeg.AVERROR_EOF, reader.Read(buffer, 16));
            Assert.Equal(2, reader.TakeStarted().Count);

            var next = new ChunkReader(queue, 1, CancellationToken.None);

            Assert.Equal(1, next.Read(buffer, 16));
            Assert.Equal(9, buffer[0]);
            Assert.Equal(ffmpeg.AVERROR_EOF, next.Read(buffer, 16));
            Assert.True(queue.IsCompleted);
        }

        [Fact]
        public void Timeline_StartsAtZeroAndJoinsRuns()
        {
            var timeline = new Timeline();

            var first = timeline.Offset(40, 90000.5);

            Assert.Equal(0, 90000.5 + first, 6);

            timeline.NoteEnd(12.0);

            Assert.Equal(first, timeline.Offset(40, 90010));

            var second = timeline.Offset(41, 5.0);

            Assert.Equal(12.0, 5.0 + second, 6);

            Assert.True(timeline.IsJump(3.0, 12.0));
            Assert.True(timeline.IsJump(40.0, 12.0));
            Assert.False(timeline.IsJump(12.04, 12.0));
            Assert.False(timeline.IsJump(5.0, double.NaN));
        }
    }
}
