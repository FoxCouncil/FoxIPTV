// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using Classes;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class IPTVDotOrg : IService
    {
        public string Id => "iptv-org";

        public string Title { get; } = "IPTV.org";

        public string Description => "Every publicly listed channel in the iptv-org database, no account needed";

        public ProviderCapabilities Capabilities => ProviderCapabilities.LiveTv;

        public List<ProviderField> Fields { get; } = new List<ProviderField> { ProviderParts.RegionField("all") };

        public JObject Data { get; set; }

        public bool SaveAuthentication { get; set; }

        public Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        public Task<bool> IsAuthenticated()
        {
            return Task.FromResult(true);
        }

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

            var rows = JArray.Parse(ProviderParts.ShippedList("iptv-org.json.gz"));
            var region = ProviderParts.Setting(Data, "Region", "all").ToUpperInvariant();
            var country = region == "GB" ? "UK" : region;

            progressPercentage.Report(20);

            var channelList = new List<Channel>();

            var channelNumber = 0u;

            var totalItems = rows.Count;
            var processed = 0;

            foreach (var row in rows)
            {
                if (++processed % 500 == 0)
                {
                    await Task.Yield();

                    progressPercentage.Report(20 + (int)(processed / (float)totalItems * 80));
                }

                if (country != "ALL" && !string.Equals(row[2]?.ToString(), country, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!Uri.TryCreate(row[4]?.ToString(), UriKind.Absolute, out var streamUri))
                {
                    continue;
                }

                channelList.Add(new Channel
                {
                    Index = ++channelNumber,
                    Id = row[0]?.ToString(),
                    Name = $"{row[2]}: {row[1]}",
                    Group = row[3]?.ToString(),
                    Stream = streamUri
                });
            }

            progressPercentage.Report(100);

            TvCore.LogDebug($"[{Title}] ProcessChannels() End: {channelList.Count} channel(s) processed");

            return channelList;
        }
    }
}
