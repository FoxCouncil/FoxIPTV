// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Classes;
    using Newtonsoft.Json.Linq;

    public class SamsungTvPlus : IService
    {
        public string Id => "samsungtvplus";

        public string Title => "Samsung TV Plus";

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

        public Task<Tuple<List<Channel>, List<Programme>>> Process()
        {
            ProgressUpdater?.Item1.Report(0);

            var rows = JArray.Parse(ProviderParts.ShippedList("samsungtvplus.json.gz"));
            var offered = rows.Select(x => x[0]?.ToString()).Where(x => !string.IsNullOrEmpty(x)).Distinct().Append("all").ToList();
            var region = ProviderParts.Region(Data, offered, Title);
            var channels = new List<Channel>();

            foreach (var row in rows)
            {
                if (region != "all" && row[0]?.ToString() != region)
                {
                    continue;
                }

                if (!Uri.TryCreate(row[7]?.ToString(), UriKind.Absolute, out var stream) || ProviderParts.IsUnwanted(row[3]?.ToString()))
                {
                    continue;
                }

                var group = (region == "all" ? row[5] : row[4])?.ToString();

                channels.Add(new Channel
                {
                    Index = uint.TryParse(row[2]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0,
                    Id = row[1]?.ToString() ?? string.Empty,
                    Name = row[3]?.ToString() ?? stream.Host,
                    Group = string.IsNullOrWhiteSpace(group) ? Title : group,
                    Logo = Uri.TryCreate(row[6]?.ToString(), UriKind.Absolute, out var logo) ? logo : null,
                    Stream = stream
                });
            }

            ProgressUpdater?.Item1.Report(100);

            TvCore.LogInfo($"[{Title}] {channels.Count} channels for region {region}; no guide available for this source");

            return Task.FromResult(new Tuple<List<Channel>, List<Programme>>(ProviderParts.Numbered(channels), new List<Programme>()));
        }
    }
}
