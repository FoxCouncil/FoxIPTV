// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests.Engine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using FoxIPTV.Classes;
    using FoxIPTV.Playback;

    [Collection("AdDetector")]
    public sealed class EngineTests : IDisposable
    {
        private const string Ad = "_ad/creative/0123456789abcdef0123";

        private const double FrameRate = 25;

        private const int LiveWindow = 4;

        private static readonly string Media = Path.Combine(AppContext.BaseDirectory, "Media");

        private readonly MediaServer _server = new MediaServer();

        public EngineTests()
        {
            Assert.True(FFmpegNative.Initialize(), FFmpegNative.Failure);

            AdDetector.Reset();
        }

        public void Dispose()
        {
            _server.Dispose();
        }

        [Fact]
        public void Vod_PlaysToTheEndInTime()
        {
            ServeClips("low", 4);
            _server.Serve("/vod.m3u8", Vod(Pieces("low", 4)));

            using var run = Play("/vod.m3u8", false);

            Assert.True(run.WaitFor(PlayerState.Ended, 20), run.Describe());

            AssertSmooth(run, 4, 160, 90);
            AssertPolite();
        }

        [Fact]
        public void Live_JoinsAnAdBreakWithoutStopping()
        {
            var pieces = new[]
            {
                new LivePiece("low/0.ts"), new LivePiece("low/1.ts"), new LivePiece("low/2.ts"), new LivePiece("low/3.ts"),
                new LivePiece($"{Ad}/0.ts", true), new LivePiece($"{Ad}/1.ts"),
                new LivePiece("low/0.ts", true), new LivePiece("low/1.ts"), new LivePiece("low/2.ts"), new LivePiece("low/3.ts")
            };

            ServeLive(pieces, null);

            using var run = Play("/live.m3u8", true);

            Assert.True(run.WaitFor(PlayerState.Ended, 30), run.Describe());

            AssertSmooth(run, 9, 160, 90);
            Assert.True(run.AdSeen, "the ad break was not noticed");
            Assert.False(AdDetector.InAd, "the ad break did not end");
            AssertPolite();
        }

        [Fact]
        public void Live_SamsungStayTunedSlateShowsAsAnAd()
        {
            const string show = "live-content/260/content/SHOW/1";
            const string programme = "pid=260";

            var pieces = new[]
            {
                new LivePiece("low/0.ts", false, $"{show}/6_000.ts", programme),
                new LivePiece("low/1.ts", false, $"{show}/6_001.ts", programme),
                new LivePiece("low/2.ts", false, $"{show}/6_002.ts", programme),
                new LivePiece("low/3.ts", false, $"{show}/split_202609280000/6_003_a.ts", programme),
                new LivePiece($"{Ad}/0.ts", true, "live-content/149/modified_bumpers/149/content/BUMPER/1/6_000.ts"),
                new LivePiece($"{Ad}/1.ts", true, "live-content/149/content/SLATE/1/6_000.ts"),
                new LivePiece("low/0.ts", true, $"{show}/split_202609280000/6_003_b.ts", programme),
                new LivePiece("low/1.ts", false, $"{show}/6_004.ts", programme),
                new LivePiece("low/2.ts", false, $"{show}/6_005.ts", programme),
                new LivePiece("low/3.ts", false, $"{show}/6_006.ts", programme)
            };

            ServeLive(pieces, null);

            using var run = Play("/live.m3u8", true);

            Assert.True(run.WaitFor(PlayerState.Ended, 30), run.Describe());

            AssertSmooth(run, 9, 160, 90);
            Assert.True(run.AdSeen, "the slate was not shown as an ad");
            Assert.False(AdDetector.InAd, "the break did not end when the programme came back");
            AssertPolite();
        }

        [Fact]
        public void Live_SkipsAMissingPiece()
        {
            var pieces = new[]
            {
                new LivePiece("low/0.ts"), new LivePiece("low/1.ts"), new LivePiece("low/2.ts"), new LivePiece("low/3.ts"),
                new LivePiece("low/0.ts", true), new LivePiece("low/1.ts"), new LivePiece("low/2.ts"), new LivePiece("low/3.ts")
            };

            ServeLive(pieces, 6);

            using var run = Play("/live.m3u8", true);

            Assert.True(run.WaitFor(PlayerState.Ended, 30), run.Describe());

            var frames = run.Frames;

            Assert.True(frames.Count >= 6 * FrameRate * 0.9, $"{frames.Count} frames. {run.Describe()}");
            Assert.Contains(_server.Requests, x => x.Path == "/live/6.ts");
            Assert.Contains(_server.Requests, x => x.Path == "/live/7.ts");
            AssertIncreasing(frames);
            AssertPolite();
        }

        [Fact]
        public void Aes128_DecryptsEveryPiece()
        {
            var key = Enumerable.Range(0, 16).Select(x => (byte)x).ToArray();
            var iv = Enumerable.Range(16, 16).Select(x => (byte)x).ToArray();

            using (var aes = Aes.Create())
            {
                aes.Key = key;

                for (var i = 0; i < 4; i++)
                {
                    _server.Serve($"/secret/{i}.ts", aes.EncryptCbc(Clip($"low/{i}.ts"), iv, PaddingMode.PKCS7));
                }
            }

            _server.Serve("/secret.key", key);
            _server.Serve("/secret.m3u8", Vod(Pieces("secret", 4), "#EXT-X-KEY:METHOD=AES-128,URI=\"secret.key\",IV=0x101112131415161718191A1B1C1D1E1F"));

            using var run = Play("/secret.m3u8", false);

            Assert.True(run.WaitFor(PlayerState.Ended, 20), run.Describe());

            AssertSmooth(run, 4, 160, 90);
            AssertPolite();
        }

        [Fact]
        public void Master_DropsQualityWhenPiecesAreSlow()
        {
            ServeClips("low", 4);

            for (var i = 0; i < 4; i++)
            {
                var data = Clip($"high/{i}.ts");

                _server.Serve($"/high/{i}.ts", () => new Reply { Data = data, Delay = 0.9 });
            }

            _server.Serve("/low.m3u8", Vod(Pieces("low", 4)));
            _server.Serve("/high.m3u8", Vod(Pieces("high", 4)));
            _server.Serve("/master.m3u8", "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=150000,RESOLUTION=160x90,CODECS=\"avc1.42c00c,mp4a.40.2\"\nlow.m3u8\n#EXT-X-STREAM-INF:BANDWIDTH=600000,RESOLUTION=320x180,CODECS=\"avc1.42c00d,mp4a.40.2\"\nhigh.m3u8\n");

            using var run = Play("/master.m3u8", false);

            Assert.True(run.WaitFor(PlayerState.Ended, 25), run.Describe());

            var widths = run.Frames.Select(x => x.Width).ToList();
            var firstLow = widths.IndexOf(160);

            Assert.True(widths.Count > 0 && widths[0] == 320, $"started at {(widths.Count > 0 ? widths[0] : 0)} wide. {run.Describe()}");
            Assert.True(firstLow > 0, $"never dropped to the low quality. {run.Describe()}");
            Assert.DoesNotContain(320, widths.Skip(firstLow));
            AssertIncreasing(run.Frames);
            AssertPolite();
        }

        [Fact]
        public void Fmp4_PlaysWithAnInitSection()
        {
            _server.Serve("/fmp4/init.mp4", Clip("fmp4/init.mp4"));
            ServeClips("fmp4", 2, "m4s");
            _server.Serve("/fmp4.m3u8", Vod(Pieces("fmp4", 2, "m4s"), "#EXT-X-MAP:URI=\"fmp4/init.mp4\""));

            using var run = Play("/fmp4.m3u8", false);

            Assert.True(run.WaitFor(PlayerState.Ended, 20), run.Describe());

            AssertSmooth(run, 2, 160, 90);
            AssertPolite();
        }

        [Fact]
        public void Progressive_PlaysAFileToTheEnd()
        {
            _server.Serve("/file.ts", Enumerable.Range(0, 4).SelectMany(i => Clip($"low/{i}.ts")).ToArray());

            using var run = Play("/file.ts", false);

            Assert.True(run.WaitFor(PlayerState.Ended, 20), run.Describe());

            AssertSmooth(run, 4, 160, 90);
            AssertPolite();
        }

        [Fact]
        public void Progressive_OnDemandWithNoLengthEndsOnce()
        {
            var stream = Enumerable.Range(0, 4).SelectMany(i => Clip($"low/{i}.ts")).ToArray();

            _server.Serve("/movie.ts", () => new Reply { Data = stream, Chunked = true });

            using var run = Play("/movie.ts", false);

            Assert.True(run.WaitFor(PlayerState.Ended, 20), run.Describe());

            AssertSmooth(run, 4, 160, 90);
            Assert.Single(_server.Requests, x => x.Path == "/movie.ts");
            AssertPolite();
        }

        [Fact]
        public void Progressive_LiveKeepsPlayingWhenTheServerHangsUp()
        {
            var stream = Enumerable.Range(0, 4).SelectMany(i => Clip($"low/{i}.ts")).ToArray();

            _server.Serve("/stream.ts", () => new Reply { Data = stream, Chunked = true });

            using var run = Play("/stream.ts", true);

            Assert.True(run.WaitFor(PlayerState.Playing, 10), run.Describe());
            Assert.True(SpinWait.SpinUntil(() => run.Frames.Count > 0 && run.Frames[^1].Time >= 9, TimeSpan.FromSeconds(20)), run.Describe());

            var frames = run.Frames;
            var widestStep = frames.Zip(frames.Skip(1), (a, b) => b.Time - a.Time).Max();

            Assert.True(_server.Requests.Count(x => x.Path == "/stream.ts") >= 3, $"only {_server.Requests.Count(x => x.Path == "/stream.ts")} connections");
            Assert.True(frames.Count >= 9 * FrameRate * 0.9, $"{frames.Count} frames. {run.Describe()}");
            Assert.True(widestStep < 3 / FrameRate, $"a {widestStep:0.000}s hole between frames. {run.Describe()}");
            Assert.DoesNotContain(PlayerState.Buffering, run.States);
            AssertIncreasing(frames);
            AssertPolite();
        }

        private PlayerRun Play(string path, bool live)
        {
            return new PlayerRun(new MediaRequest { Uri = _server.Url(path), IsLive = live, Label = path });
        }

        private static byte[] Clip(string path)
        {
            return File.ReadAllBytes(Path.Combine(Media, path));
        }

        private void ServeClips(string folder, int count, string extension = "ts")
        {
            for (var i = 0; i < count; i++)
            {
                _server.Serve($"/{folder}/{i}.{extension}", Clip($"{folder}/{i}.{extension}"));
            }
        }

        private static IEnumerable<string> Pieces(string folder, int count, string extension = "ts")
        {
            return Enumerable.Range(0, count).Select(i => $"{folder}/{i}.{extension}");
        }

        private static string Vod(IEnumerable<string> pieces, string header = null)
        {
            var text = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:7\n#EXT-X-TARGETDURATION:1\n#EXT-X-MEDIA-SEQUENCE:0\n#EXT-X-PLAYLIST-TYPE:VOD\n");

            if (header != null)
            {
                text.Append(header).Append('\n');
            }

            foreach (var piece in pieces)
            {
                text.Append("#EXTINF:1.000,\n").Append(piece).Append('\n');
            }

            return text.Append("#EXT-X-ENDLIST\n").ToString();
        }

        private void ServeLive(IReadOnlyList<LivePiece> pieces, int? missing)
        {
            var started = double.NaN;
            var paths = pieces.Select((piece, i) => piece.Path ?? (piece.Clip.StartsWith(Ad, StringComparison.Ordinal) ? $"live/{Ad}/{i}.ts" : $"live/{i}.ts")).ToList();

            for (var i = 0; i < pieces.Count; i++)
            {
                var data = Clip(pieces[i].Clip);
                Func<Reply> reply = i == missing ? Reply.NotFound : () => new Reply { Data = data };

                _server.Serve("/" + paths[i], reply);
            }

            _server.Serve("/live.m3u8", () =>
            {
                if (double.IsNaN(started))
                {
                    started = _server.Now;
                }

                var available = Math.Min(pieces.Count, LiveWindow + (int)(_server.Now - started));
                var first = available - LiveWindow;
                var text = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:1\n");

                text.Append($"#EXT-X-MEDIA-SEQUENCE:{first}\n");
                text.Append($"#EXT-X-DISCONTINUITY-SEQUENCE:{pieces.Take(first + 1).Count(x => x.StartsDiscontinuity)}\n");

                for (var i = first; i < available; i++)
                {
                    if (i > first && pieces[i].StartsDiscontinuity)
                    {
                        text.Append("#EXT-X-DISCONTINUITY\n");
                    }

                    text.Append("#EXTINF:1.000,").Append(pieces[i].Title).Append('\n').Append(paths[i]).Append('\n');
                }

                if (available == pieces.Count)
                {
                    text.Append("#EXT-X-ENDLIST\n");
                }

                return new Reply { Data = Encoding.UTF8.GetBytes(text.ToString()) };
            });
        }

        private static void AssertIncreasing(IReadOnlyList<ShownFrame> frames)
        {
            for (var i = 1; i < frames.Count; i++)
            {
                Assert.True(frames[i].Time > frames[i - 1].Time, $"frame {i} at {frames[i].Time:0.000}s came after {frames[i - 1].Time:0.000}s");
            }
        }

        private static void AssertSmooth(PlayerRun run, double seconds, int width, int height)
        {
            var frames = run.Frames;
            var timed = frames.Where(x => !double.IsNaN(x.Clock)).ToList();
            var span = frames.Count == 0 ? 0 : frames[^1].Time - frames[0].Time;
            var widestStep = frames.Zip(frames.Skip(1), (a, b) => b.Time - a.Time).DefaultIfEmpty(0).Max();
            var offSound = timed.Count(x => Math.Abs(x.Clock - x.Time) > 0.1);

            Assert.True(frames.Count >= seconds * FrameRate * 0.9, $"{frames.Count} frames for {seconds}s. {run.Describe()}");
            Assert.All(frames, x => Assert.Equal((width, height), (x.Width, x.Height)));
            AssertIncreasing(frames);
            Assert.True(widestStep < 3 / FrameRate, $"a {widestStep:0.000}s hole between frames. {run.Describe()}");
            Assert.True(Math.Abs(span - seconds) < 0.5, $"frames cover {span:0.00}s, expected {seconds}s. {run.Describe()}");
            Assert.True(offSound <= timed.Count / 20, $"{offSound} of {timed.Count} frames shown more than 0.1s off the sound. {run.Describe()}");
            Assert.DoesNotContain(PlayerState.Buffering, run.States.SkipWhile(x => x != PlayerState.Playing));
            Assert.Equal("aac", run.Player.Info.AudioCodec);
        }

        private void AssertPolite()
        {
            var requests = _server.Requests;

            Assert.NotEmpty(requests);

            foreach (var request in requests)
            {
                Assert.StartsWith("Mozilla/5.0 (", request.UserAgent ?? string.Empty);
                Assert.DoesNotContain("fox", request.UserAgent, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("iptv", request.UserAgent, StringComparison.OrdinalIgnoreCase);
            }

            foreach (var group in requests.GroupBy(x => x.Path))
            {
                var ordered = group.OrderBy(x => x.Started).ToList();

                for (var i = 1; i < ordered.Count; i++)
                {
                    var previousEnd = double.IsNaN(ordered[i - 1].Finished) ? double.PositiveInfinity : ordered[i - 1].Finished;

                    Assert.True(ordered[i].Started >= previousEnd - 0.05, $"two requests for {group.Key} at once");
                }
            }
        }

        private readonly record struct LivePiece(string Clip, bool StartsDiscontinuity = false, string Path = null, string Title = null);
    }
}
