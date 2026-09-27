// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System.Linq;
    using FoxIPTV.Classes;
    using FoxIPTV.Services;
    using FoxIPTV.Services.Scripting;
    using LibVLCSharp.Shared;

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
        public void TvIconData_ReadsTracks()
        {
            var video = new MediaTrack();
            var audio = new MediaTrack();

            video = SetTrack(video, TrackType.Video, "h264");
            audio = SetTrack(audio, TrackType.Audio, "mp4a");

            var data = TvIconData.CreateData(true, new[] { video, audio });

            Assert.True(data.ClosedCaptioning);
            Assert.Equal("VC_H264", data.VideoCodec);
            Assert.Equal("AC_MP4A", data.AudioCodec);
        }

        private static MediaTrack SetTrack(MediaTrack track, TrackType type, string fourCc)
        {
            var boxed = (object)track;
            var trackType = typeof(MediaTrack).GetField("TrackType") ?? typeof(MediaTrack).GetField("<TrackType>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var codec = typeof(MediaTrack).GetField("Codec") ?? typeof(MediaTrack).GetField("<Codec>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            trackType.SetValue(boxed, type);
            codec.SetValue(boxed, (uint)(fourCc[0] | fourCc[1] << 8 | fourCc[2] << 16 | fourCc[3] << 24));

            return (MediaTrack)boxed;
        }
    }
}
