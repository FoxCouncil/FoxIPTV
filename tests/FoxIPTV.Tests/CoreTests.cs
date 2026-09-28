// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System.Linq;
    using FoxIPTV.Classes;
    using FoxIPTV.Playback;
    using FoxIPTV.Services;
    using FoxIPTV.Services.Scripting;

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
            Assert.Contains(providers, x => x.Id == "freetv" && x.Capabilities.HasFlag(ProviderCapabilities.LiveTv));
            Assert.Contains(providers, x => x.Id == "m3u");
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
