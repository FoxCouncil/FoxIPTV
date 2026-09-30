// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Classes;
    using Newtonsoft.Json.Linq;

    public class PlutoTv : IService, ILiveTuner
    {
        private const string BootAddress = "https://boot.pluto.tv/v4/start";

        private const string GuideAddress = "https://service-channels.clusters.pluto.tv/v2/guide/";

        private const string ChannelAddress = "https://service-stitcher.clusters.pluto.tv/v2/stitch/hls/channel/";

        private const int RenewSeconds = 28800;

        private const int GuideMinutes = 1440;

        private const int GuideBatch = 100;

        private readonly SemaphoreSlim _sessionGate = new SemaphoreSlim(1, 1);

        private readonly string _clientId = Guid.NewGuid().ToString();

        private Session _session;

        public string Id => "pluto";

        public string Title => "Pluto TV";

        public string Description => string.Empty;

        public ProviderCapabilities Capabilities => ProviderCapabilities.LiveTv;

        public List<ProviderField> Fields { get; } = new List<ProviderField> { ProviderParts.RegionField() };

        public JObject Data { get; set; }

        public bool SaveAuthentication { get; set; }

        public Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        public bool CanTune => true;

        public Task<bool> IsAuthenticated()
        {
            return Task.FromResult(true);
        }

        public async Task<Tuple<List<Channel>, List<Programme>>> Process()
        {
            ProgressUpdater?.Item1.Report(0);

            var headers = await SignedHeaders().ConfigureAwait(false);
            var list = JObject.Parse(await Web.GetString(GuideAddress + "channels?channelIds=&offset=0&limit=1000&sort=number%3Aasc", headers).ConfigureAwait(false))["data"] as JArray ?? new JArray();
            var categories = JObject.Parse(await Web.GetString(GuideAddress + "categories", headers).ConfigureAwait(false))["data"] as JArray ?? new JArray();
            var groups = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var category in categories)
            {
                foreach (var id in category["channelIDs"] ?? new JArray())
                {
                    groups.TryAdd(id.ToString(), category["name"]?.ToString());
                }
            }

            var channels = new List<Channel>();

            foreach (var item in list)
            {
                var id = item["id"]?.ToString();

                if (string.IsNullOrEmpty(id) || ProviderParts.IsUnwanted(item["name"]?.ToString()))
                {
                    continue;
                }

                channels.Add(new Channel
                {
                    Index = uint.TryParse(item["number"]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0,
                    Id = id,
                    Name = item["name"]?.ToString() ?? id,
                    Group = groups.TryGetValue(id, out var group) && !string.IsNullOrWhiteSpace(group) ? group : Title,
                    Logo = Logo(item),
                    Stream = new Uri(ChannelAddress + id + "/master.m3u8")
                });
            }

            ProgressUpdater?.Item1.Report(100);

            TvCore.LogInfo($"[{Title}] {channels.Count} channels");

            var programmes = await Guide(channels.Select(x => x.Id).ToList(), headers).ConfigureAwait(false);

            return new Tuple<List<Channel>, List<Programme>>(ProviderParts.Numbered(channels), programmes);
        }

        public async Task<Uri> Tune(Channel channel)
        {
            if (string.IsNullOrEmpty(channel?.Id))
            {
                return null;
            }

            var session = await CurrentSession().ConfigureAwait(false);

            return new Uri($"{session.Stitcher}/v2/stitch/hls/channel/{channel.Id}/master.m3u8?{session.Parameters}&jwt={session.Token}&masterJWTPassthrough=true");
        }

        private async Task<List<Programme>> Guide(List<string> ids, Dictionary<string, string> headers)
        {
            var start = DateTime.UtcNow;
            var programmes = new List<Programme>();

            start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute < 30 ? 0 : 30, 0, DateTimeKind.Utc);

            for (var i = 0; i < ids.Count; i += GuideBatch)
            {
                var batch = string.Join(",", ids.Skip(i).Take(GuideBatch));
                var address = $"{GuideAddress}timelines?start={start:yyyy-MM-ddTHH:mm:ss.000Z}&channelIds={batch}&duration={GuideMinutes}";
                var data = JObject.Parse(await Web.GetString(address, headers).ConfigureAwait(false))["data"] as JArray ?? new JArray();

                foreach (var channel in data)
                {
                    foreach (var entry in channel["timelines"] ?? new JArray())
                    {
                        if (DateTimeOffset.TryParse(entry["start"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var from) && DateTimeOffset.TryParse(entry["stop"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var to) && to > from)
                        {
                            programmes.Add(ProviderParts.Programme(channel["channelId"]?.ToString(), from, to, entry["title"]?.ToString(), entry["episode"]?["description"]?.ToString()));
                        }
                    }
                }

                ProgressUpdater?.Item2.Report(Math.Min(100, (i + GuideBatch) * 100 / Math.Max(1, ids.Count)));
            }

            return programmes;
        }

        private async Task<Dictionary<string, string>> SignedHeaders()
        {
            var session = await CurrentSession().ConfigureAwait(false);

            return new Dictionary<string, string> { ["Origin"] = "https://pluto.tv", ["Referer"] = "https://pluto.tv/", ["Authorization"] = "Bearer " + session.Token };
        }

        private async Task<Session> CurrentSession()
        {
            await _sessionGate.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_session != null && DateTime.UtcNow < _session.RenewAt)
                {
                    return _session;
                }

                var query = string.Join("&", "appName=web", "appVersion=9.1.2", "deviceVersion=128.0.0", "deviceModel=web", "deviceMake=chrome", "deviceType=web", "clientID=" + _clientId, "clientModelNumber=1.0.0", "serverSideAds=false", "drmCapabilities=", "blockingMode=", "notificationVersion=1", "appLaunchCount=0", "lastAppLaunchDate=");
                var boot = JObject.Parse(await Web.GetString(BootAddress + "?" + query, new Dictionary<string, string> { ["Origin"] = "https://pluto.tv", ["Referer"] = "https://pluto.tv/" }).ConfigureAwait(false));
                var renew = Math.Min(boot["refreshInSec"]?.Value<int?>() ?? RenewSeconds, RenewSeconds);
                var active = boot["session"]?["activeRegion"]?.ToString()?.ToLowerInvariant() ?? string.Empty;

                _session = new Session
                {
                    Token = boot["sessionToken"]?.ToString(),
                    Stitcher = boot["servers"]?["stitcher"]?.ToString(),
                    Parameters = boot["stitcherParams"]?.ToString(),
                    RenewAt = DateTime.UtcNow.AddSeconds(renew)
                };

                var wanted = ProviderParts.Setting(Data, "Region", "us").ToLowerInvariant();

                if (active.Length > 0 && wanted != active)
                {
                    TvCore.LogInfo($"[{Title}] Region '{wanted}' not offered by this source, using '{active}'");
                }

                TvCore.LogInfo($"[{Title}] Session started, region {active}");

                return _session;
            }
            finally
            {
                _sessionGate.Release();
            }
        }

        private static Uri Logo(JToken channel)
        {
            var images = channel["images"] as JArray ?? new JArray();
            var chosen = images.FirstOrDefault(x => x["type"]?.ToString() == "colorLogoPNG") ?? images.FirstOrDefault();

            return Uri.TryCreate(chosen?["url"]?.ToString(), UriKind.Absolute, out var logo) ? logo : null;
        }

        private sealed class Session
        {
            public string Token { get; set; }

            public string Stitcher { get; set; }

            public string Parameters { get; set; }

            public DateTime RenewAt { get; set; }
        }
    }
}
