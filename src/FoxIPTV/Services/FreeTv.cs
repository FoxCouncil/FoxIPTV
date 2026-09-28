// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Classes;
    using Newtonsoft.Json.Linq;

    public class FreeTv : IService
    {
        private const string ListAddress = "https://raw.githubusercontent.com/Free-TV/IPTV/master/playlist.m3u8";

        private const double CacheHours = 6;

        private static readonly string[] OfferedRegions = { "all" };

        public string Id => "freetv";

        public string Title => "Free-TV";

        public string Description => string.Empty;

        public ProviderCapabilities Capabilities => ProviderCapabilities.LiveTv;

        public List<ProviderField> Fields { get; } = new List<ProviderField> { ProviderParts.RegionField() };

        public JObject Data { get; set; }

        public bool SaveAuthentication { get; set; }

        public Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        public Task<bool> IsAuthenticated()
        {
            return Task.FromResult(true);
        }

        public async Task<Tuple<List<Channel>, List<Programme>>> Process()
        {
            ProgressUpdater?.Item1.Report(0);

            var region = ProviderParts.Region(Data, OfferedRegions, Title);
            var playlist = M3UParser.Parse(await Web.GetStringCached(ListAddress, "list-" + ListAddress.ToMD5(), CacheHours).ConfigureAwait(false));
            var channels = ProviderParts.FromPlaylist(playlist);

            ProgressUpdater?.Item1.Report(100);

            var guideAddress = PickGuide(playlist.GuideUrls, region);

            if (guideAddress == null)
            {
                TvCore.LogInfo($"[{Title}] No guide available for this source and region");

                return new Tuple<List<Channel>, List<Programme>>(channels, new List<Programme>());
            }

            var guide = XmltvParser.Parse(await Web.GetStringCached(guideAddress, "list-" + guideAddress.ToMD5(), CacheHours).ConfigureAwait(false), ProgressUpdater?.Item2);

            return new Tuple<List<Channel>, List<Programme>>(channels, guide);
        }

        private static string PickGuide(List<string> addresses, string region)
        {
            if (addresses.Count <= 1)
            {
                return addresses.FirstOrDefault();
            }

            var tag = "_" + region.ToUpperInvariant();

            return addresses.FirstOrDefault(x => x.Contains(tag + "1.", StringComparison.Ordinal) || x.Contains(tag + ".", StringComparison.Ordinal));
        }
    }
}
