// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using Classes;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>The iptv-org community database of publicly available channels, via its JSON API</summary>
    public class IPTVDotOrg : IService
    {
        private const int CacheTimeChannelsInHours = 12;
        private const int CacheTimeStreamsInHours = 12;

        private const string CacheFilenameChannel = "iptv-cdata";
        private const string CacheFilenameStreams = "iptv-sdata";

        private const string UrlChannels = "https://iptv-org.github.io/api/channels.json";
        private const string UrlStreams = "https://iptv-org.github.io/api/streams.json";

        /// <inheritdoc/>
        public string Id => "iptv-org";

        /// <inheritdoc/>
        public string Title { get; } = "IPTV.org";

        /// <inheritdoc/>
        public string Description => "Every publicly listed channel in the iptv-org database, no account needed";

        /// <inheritdoc/>
        public ProviderCapabilities Capabilities => ProviderCapabilities.LiveTv;

        /// <inheritdoc/>
        public List<ProviderField> Fields { get; } = new List<ProviderField>();

        /// <inheritdoc/>
        public JObject Data { get; set; }

        /// <inheritdoc/>
        public bool SaveAuthentication { get; set; }

        /// <inheritdoc/>
        public Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        /// <inheritdoc/>
        public Task<bool> IsAuthenticated()
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public async Task<Tuple<List<Channel>, List<Programme>>> Process()
        {
            var item1 = await ProcessChannels();

            ProgressUpdater?.Item2.Report(100);

            return new Tuple<List<Channel>, List<Programme>>(item1, new List<Programme>());
        }

        private async Task<List<Channel>> ProcessChannels()
        {
            TvCore.LogDebug($"[{Title}] ProcessChannels() Start...");

            var progressPercentage = ProgressUpdater.Item1;

            progressPercentage.Report(0);

            var channelDataRaw = await TvCore.DownloadStringAndCache(UrlChannels, CacheFilenameChannel, CacheTimeChannelsInHours);
            var channelData = JArray.Parse(channelDataRaw).ToDictionary(x => x["id"]?.ToString() ?? string.Empty, x => x);

            progressPercentage.Report(10);

            var streamsDataRaw = await TvCore.DownloadStringAndCache(UrlStreams, CacheFilenameStreams, CacheTimeStreamsInHours);
            var streamsData = JArray.Parse(streamsDataRaw);

            progressPercentage.Report(20);

            var channelList = new List<Channel>();

            var channelNumber = 0u;

            var totalItems = streamsData.Count;
            var processed = 0;

            foreach (var stream in streamsData)
            {
                if (++processed % 500 == 0)
                {
                    await Task.Yield();

                    progressPercentage.Report(20 + (int)(processed / (float)totalItems * 80));
                }

                var channelName = stream["channel"]?.ToString();

                if (string.IsNullOrEmpty(channelName))
                {
                    continue;
                }

                var channelUrl = stream["url"]?.ToString();

                if (string.IsNullOrEmpty(channelUrl) || channelUrl == "undefined" || !Uri.TryCreate(channelUrl, UriKind.Absolute, out var streamUri))
                {
                    continue;
                }

                if (!channelData.TryGetValue(channelName, out var channel))
                {
                    continue;
                }

                var categories = channel["categories"];
                var category = categories != null && categories.HasValues ? categories.First().ToString() : "none";

                var channelFullname = channel["name"]?.ToString();

                var channelCountry = channel["country"]?.ToString();

                channelList.Add(new Channel
                {
                    Index = ++channelNumber,
                    Id = channelName,
                    Name = $"{channelCountry}: {channelFullname}",
                    Group = category,
                    Logo = Uri.TryCreate(channel["logo"]?.ToString(), UriKind.Absolute, out var result) ? result : null,
                    Stream = streamUri
                });
            }

            progressPercentage.Report(100);

            TvCore.LogDebug($"[{Title}] ProcessChannels() End: {channelList.Count} channel(s) processed");

            return channelList;
        }
    }
}
