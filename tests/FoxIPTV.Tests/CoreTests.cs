// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System.IO;
    using System.Linq;
    using FoxIPTV.Classes;
    using FoxIPTV.Playback;
    using FoxIPTV.Services;

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
        public void Providers_KeepTheirIdsAndRegion()
        {
            IService[] providers = { new PlutoTv(), new PlexTv(), new FreeTv(), new M3uPlaylist() };

            Assert.Equal(new[] { "pluto", "plex", "freetv", "m3u" }, providers.Select(x => x.Id));
            Assert.All(providers, x => Assert.True(x.Capabilities.HasFlag(ProviderCapabilities.LiveTv)));
            Assert.All(providers.Take(3), x => Assert.Contains(x.Fields, f => f.Key == "Region" && f.Default == "us" && f.Choices.Count == 21));
            Assert.Equal(new[] { "Playlist URL", "Guide URL", "Cache Hours" }, providers[3].Fields.Select(x => x.Key));
            Assert.True(((ILiveTuner)providers[0]).CanTune);
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
