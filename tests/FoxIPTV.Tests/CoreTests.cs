// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
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
            IService[] providers = { new PlutoTv(), new PlexTv(), new RokuTv(), new FreeTv(), new M3uPlaylist(), new IPTVDotOrg() };

            Assert.Equal(new[] { "pluto", "plex", "roku", "freetv", "m3u", "iptv-org" }, providers.Select(x => x.Id));
            Assert.All(providers, x => Assert.True(x.Capabilities.HasFlag(ProviderCapabilities.LiveTv)));
            Assert.All(providers.Take(4), x => Assert.Contains(x.Fields, f => f.Key == "Region" && f.Default == "us" && f.Choices.Count == 21));
            Assert.Contains(providers[5].Fields, f => f.Key == "Region" && f.Default == "all" && f.Choices.Count == 21);
            Assert.Equal(new[] { "Playlist URL", "Guide URL", "Cache Hours" }, providers[4].Fields.Select(x => x.Key));
            Assert.True(((ILiveTuner)providers[0]).CanTune);
            Assert.True(((ILiveTuner)providers[2]).CanTune);
        }

        [Fact]
        public void Roku_KeepsOnlyChannelsWithoutDrm()
        {
            var page = Newtonsoft.Json.Linq.JObject.Parse(@"{
                ""categoryMapping"": [
                    { ""type"": ""Utility"", ""id"": ""roku.epg.category-allchannels"", ""title"": ""All Channels"", ""collectionIds"": [ ""a"", ""b"", ""c"" ] },
                    { ""type"": ""Genre"", ""id"": ""roku.epg.cat-livefeed-tv"", ""title"": ""TV Shows"", ""collectionIds"": [ ""a"", ""b"", ""x"", ""y"" ] },
                    { ""type"": ""Genre"", ""id"": ""roku.epg.cat-crime"", ""title"": ""Crime"", ""collectionIds"": [ ""a"", ""z"" ] },
                    { ""type"": ""Genre"", ""id"": ""roku.epg.cat-epg-newly-added"", ""title"": ""Newly Added"", ""collectionIds"": [ ""a"" ] }
                ],
                ""collections"": [
                    { ""features"": { ""station"": { ""title"": ""Open"", ""displayNumber"": ""116"", ""meta"": { ""id"": ""a"", ""mediaType"": ""livefeed"" }, ""imageMap"": { ""epgLogo"": { ""path"": ""https://example.com/a.png"" } }, ""viewOptions"": [ { ""playId"": ""s-open"", ""providerId"": ""rokuavod"", ""media"": { ""videos"": [ { ""videoType"": ""HLS"", ""url"": ""https://example.com/a?format=hls"" } ] } } ] } } },
                    { ""features"": { ""station"": { ""title"": ""Locked"", ""displayNumber"": ""100"", ""meta"": { ""id"": ""b"", ""mediaType"": ""livefeed"" }, ""viewOptions"": [ { ""playId"": ""s-locked"", ""providerId"": ""rokuavod"", ""media"": { ""videos"": [ { ""videoType"": ""DASH"", ""url"": ""https://example.com/b?format=dash"", ""drmAuthentication"": { ""drmContentProvider"": ""roku"" } }, { ""videoType"": ""HLS"", ""url"": ""https://example.com/b?format=hls"", ""drmAuthentication"": { ""drmContentProvider"": ""roku"" } } ] } } ] } } },
                    { ""features"": { ""station"": { ""title"": ""Playlist"", ""displayNumber"": ""6001"", ""meta"": { ""id"": ""c"", ""mediaType"": ""playlist"" } } } }
                ]
            }");

            var channels = new RokuTv().Lineup(page);

            var channel = Assert.Single(channels);
            Assert.Equal(116u, channel.Index);
            Assert.Equal("a", channel.Id);
            Assert.Equal("Open", channel.Name);
            Assert.Equal("Crime", channel.Group);
            Assert.Equal("https://example.com/a.png", channel.Logo.ToString());
            Assert.Equal("https://therokuchannel.roku.com/watch/a", channel.Stream.ToString());
        }

        [Fact]
        public async Task ShippedLists_LoadWithoutGitHub()
        {
            var progress = Tuple.Create<IProgress<int>, IProgress<int>>(new Progress<int>(), new Progress<int>());
            var (freeTv, _) = await new FreeTv { ProgressUpdater = progress }.Process();
            var (iptvOrg, _) = await new IPTVDotOrg { ProgressUpdater = progress }.Process();
            var (iptvOrgUk, _) = await new IPTVDotOrg { ProgressUpdater = progress, Data = new Newtonsoft.Json.Linq.JObject { ["Region"] = "gb" } }.Process();

            Assert.True(freeTv.Count > 1000, $"{freeTv.Count} Free-TV channels");
            Assert.True(iptvOrg.Count > 10000, $"{iptvOrg.Count} IPTV.org channels");
            Assert.InRange(iptvOrgUk.Count, 100, 2000);
            Assert.All(iptvOrgUk, x => Assert.StartsWith("UK: ", x.Name));
            Assert.DoesNotContain(freeTv.Concat(iptvOrg), x => x.Stream.Host.Contains("github", StringComparison.OrdinalIgnoreCase) || (x.Logo?.Host.Contains("github", StringComparison.OrdinalIgnoreCase) ?? false));
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
