// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System.IO;
    using System.Linq;
    using FoxIPTV.Classes;
    using FoxIPTV.Playback;
    using FoxIPTV.Services;
    using FoxIPTV.Services.Scripting;
    using Newtonsoft.Json;

    public class CoreTests
    {
        [Fact]
        public void Protect_RoundTrips()
        {
            const string secret = "{\"Username\":\"fox\",\"Password\":\"hunter2\"}";

            var sealedText = secret.Protect();

            Assert.NotEqual(secret, sealedText);
            Assert.Equal(secret, sealedText.Unprotect());
        }

        [Fact]
        public void BuiltInPlugins_Compile()
        {
            var providers = ScriptLoader.LoadAll();

            Assert.DoesNotContain(ScriptLoader.Errors, x => x.Key.StartsWith("built-in"));

            foreach (var id in new[] { "pluto", "samsungtvplus", "plex", "roku", "freetv" })
            {
                Assert.Contains(providers, x => x.Id == id && x.Capabilities.HasFlag(ProviderCapabilities.LiveTv));
            }

            Assert.Contains(providers, x => x.Id == "m3u");
            Assert.DoesNotContain(providers.SelectMany(x => x.Fields), x => x.Key == "Source");
            Assert.True(providers.Single(x => x.Id == "pluto").CanTune);
        }

        [Fact]
        public void FreeTvEntries_MoveToTheirOwnProviders()
        {
            var folder = Path.Combine(Path.GetTempPath(), "foxiptv-move-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllText(Path.Combine(folder, "fcdata-freetv"), "[\"FilmRiseForensicFiles.us\",\"KIRODT1.us\",\"USBB3200017MY\",\"USBB3200018Q6\"]");
                File.WriteAllText(Path.Combine(folder, "hidden-freetv"), "[\"https://jmp2.uk/stvp-US1000016Q\", \"https://jmp2.uk/rok-34e2d8f442ab5fa3aed29436c2f8ed63.m3u8\", \"https://example.tv/live.m3u8\"]");

                TvCore.MoveFreeTvEntries(folder);
                TvCore.MoveFreeTvEntries(folder);

                string[] Read(string name) => JsonConvert.DeserializeObject<string[]>(File.ReadAllText(Path.Combine(folder, name)));

                Assert.Equal(new[] { "FilmRiseForensicFiles.us", "KIRODT1.us" }, Read("fcdata-freetv"));
                Assert.Equal(new[] { "USBB3200017MY", "USBB3200018Q6" }, Read("fcdata-samsungtvplus"));
                Assert.Equal(new[] { "https://example.tv/live.m3u8" }, Read("hidden-freetv"));
                Assert.Equal(new[] { "https://jmp2.uk/stvp-US1000016Q" }, Read("hidden-samsungtvplus"));
                Assert.Equal(new[] { "https://jmp2.uk/rok-34e2d8f442ab5fa3aed29436c2f8ed63.m3u8" }, Read("hidden-roku"));
                Assert.False(File.Exists(Path.Combine(folder, "drmdata-samsungtvplus")));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void TvIconData_ReadsStreamInfo()
        {
            var data = TvIconData.CreateData(new StreamInfo { Captions = true, VideoCodec = "h264", Height = 1080, FrameRate = 29.97, AudioCodec = "aac", AudioChannels = 2, AudioRate = 48000 });

            Assert.True(data.ClosedCaptioning);
            Assert.Equal("VC_H264", data.VideoCodec);
            Assert.Equal("VS_1080P", data.VideoSize);
            Assert.Equal("FR_30FPS", data.FrameRate);
            Assert.Equal("AC_AAC", data.AudioCodec);
            Assert.Equal("CH_STEREO", data.AudioChannel);
            Assert.Equal("AR_48KHZ", data.AudioRate);
        }

        [Fact]
        public void CaptionText_StripsAssMarkup()
        {
            Assert.Equal("HELLO THERE\nFRIEND", CaptionDecoder.StripAss("0,0,Default,,0,0,0,,{\\an7}HELLO THERE\\NFRIEND"));
        }
    }
}
