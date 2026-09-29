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
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class RokuTv : IService, ILiveTuner
    {
        private const string SiteAddress = "https://therokuchannel.roku.com";

        private const string NewlyAddedCategory = "roku.epg.cat-epg-newly-added";

        private const int TokenSeconds = 1800;

        private const int GuideBlockHours = 6;

        private const double GuideCacheHours = 6;

        private const int GuideRequestsAtOnce = 12;

        private readonly SemaphoreSlim _tokenGate = new SemaphoreSlim(1, 1);

        private readonly Dictionary<string, Play> _plays = new Dictionary<string, Play>(StringComparer.Ordinal);

        private string _token;

        private DateTime _tokenRenewAt;

        public string Id => "roku";

        public string Title => "Roku";

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

            var channels = Lineup(JObject.Parse(await Web.GetString(SiteAddress + "/api/v4/epg?include=viewOptions").ConfigureAwait(false)));

            ProgressUpdater?.Item1.Report(100);

            TvCore.LogInfo($"[{Title}] {channels.Count} channels for this connection's region; the Region setting is {ProviderParts.Setting(Data, "Region", "us")}");

            var programmes = await Guide(channels.Select(x => x.Id).ToList()).ConfigureAwait(false);

            return new Tuple<List<Channel>, List<Programme>>(ProviderParts.Numbered(channels), programmes);
        }

        public List<Channel> Lineup(JObject page)
        {
            var groups = new Dictionary<string, Tuple<int, string>>(StringComparer.Ordinal);

            foreach (var category in page["categoryMapping"] ?? new JArray())
            {
                if (category["type"]?.ToString() != "Genre" || category["id"]?.ToString() == NewlyAddedCategory)
                {
                    continue;
                }

                var members = category["collectionIds"] as JArray ?? new JArray();

                foreach (var member in members)
                {
                    if (!groups.TryGetValue(member.ToString(), out var held) || members.Count < held.Item1)
                    {
                        groups[member.ToString()] = Tuple.Create(members.Count, category["title"]?.ToString());
                    }
                }
            }

            var channels = new List<Channel>();

            _plays.Clear();

            foreach (var collection in page["collections"] ?? new JArray())
            {
                var station = collection["features"]?["station"];
                var id = station?["meta"]?["id"]?.ToString();

                if (string.IsNullOrEmpty(id) || station["meta"]?["mediaType"]?.ToString() != "livefeed")
                {
                    continue;
                }

                var view = (station["viewOptions"] ?? new JArray()).FirstOrDefault(x => !string.IsNullOrEmpty(x["playId"]?.ToString()) && (x["media"]?["videos"] ?? new JArray()).Any(IsOpenHls));

                if (view == null)
                {
                    continue;
                }

                channels.Add(new Channel
                {
                    Index = uint.TryParse(station["displayNumber"]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0,
                    Id = id,
                    Name = station["title"]?.ToString() ?? id,
                    Group = groups.TryGetValue(id, out var group) && !string.IsNullOrWhiteSpace(group.Item2) ? group.Item2 : Title,
                    Logo = Uri.TryCreate(station["imageMap"]?["epgLogo"]?["path"]?.ToString(), UriKind.Absolute, out var logo) ? logo : null,
                    Stream = new Uri($"{SiteAddress}/watch/{id}")
                });

                _plays[id] = new Play { PlayId = view["playId"].ToString(), ProviderId = view["providerId"]?.ToString() ?? "rokuavod" };
            }

            return channels;
        }

        public async Task<Uri> Tune(Channel channel)
        {
            if (string.IsNullOrEmpty(channel?.Id) || !_plays.TryGetValue(channel.Id, out var play))
            {
                return null;
            }

            var body = new JObject
            {
                ["rokuId"] = channel.Id,
                ["playId"] = play.PlayId,
                ["mediaFormat"] = "m3u",
                ["drmType"] = "widevine",
                ["quality"] = "fhd",
                ["bifUrl"] = JValue.CreateNull(),
                ["adPolicyId"] = string.Empty,
                ["providerId"] = play.ProviderId
            };

            var headers = new Dictionary<string, string> { ["csrf-token"] = await Token().ConfigureAwait(false) };
            var answer = JObject.Parse(await Web.PostString(SiteAddress + "/api/v3/playback", body.ToString(Formatting.None), "application/json", headers).ConfigureAwait(false));

            return Uri.TryCreate(answer["url"]?.ToString(), UriKind.Absolute, out var stream) ? stream : null;
        }

        private async Task<List<Programme>> Guide(List<string> ids)
        {
            var now = DateTime.UtcNow;
            var block = $"tpl_date={now:yyyy-MM-dd}&tpl_hour={now.Hour / GuideBlockHours * GuideBlockHours}";
            var pages = new string[ids.Count];
            var done = 0;

            using (var gate = new SemaphoreSlim(GuideRequestsAtOnce))
            {
                await Task.WhenAll(ids.Select(async (id, index) =>
                {
                    var address = $"{SiteAddress}/api/v4/epg/{id}?{block}&include=title,description,series.title&expand=series";

                    await gate.WaitAsync().ConfigureAwait(false);

                    try
                    {
                        pages[index] = await Web.GetStringCached(address, "roku-" + address.ToMD5(), GuideCacheHours).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        TvCore.LogDebug($"[{Title}] Guide page failed {address}: {ex.Message}");
                    }
                    finally
                    {
                        gate.Release();

                        var count = Interlocked.Increment(ref done);

                        if (count % 25 == 0)
                        {
                            ProgressUpdater?.Item2.Report(count * 100 / ids.Count);
                        }
                    }
                })).ConfigureAwait(false);
            }

            var programmes = new List<Programme>();

            for (var index = 0; index < pages.Length; index++)
            {
                if (pages[index] == null)
                {
                    continue;
                }

                foreach (var item in JObject.Parse(pages[index])["view"] ?? new JArray())
                {
                    var content = item["content"];
                    var slot = content?["otaOptions"]?.FirstOrDefault();
                    var begins = slot?["start"]?.Value<long?>();
                    var ends = slot?["end"]?.Value<long?>();

                    if (begins.HasValue && ends.HasValue && ends > begins)
                    {
                        programmes.Add(ProviderParts.Programme(ids[index], DateTimeOffset.FromUnixTimeSeconds(begins.Value), DateTimeOffset.FromUnixTimeSeconds(ends.Value), content["series"]?["title"]?.ToString() ?? content["title"]?.ToString(), content["description"]?.ToString()));
                    }
                }
            }

            return programmes;
        }

        private async Task<string> Token()
        {
            await _tokenGate.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_token == null || DateTime.UtcNow >= _tokenRenewAt)
                {
                    _token = JObject.Parse(await Web.GetString(SiteAddress + "/api/v1/csrf").ConfigureAwait(false))["csrf"]?.ToString();
                    _tokenRenewAt = DateTime.UtcNow.AddSeconds(TokenSeconds);
                }

                return _token;
            }
            finally
            {
                _tokenGate.Release();
            }
        }

        private static bool IsOpenHls(JToken video)
        {
            var drm = video["drmAuthentication"];

            return video["videoType"]?.ToString() == "HLS" && !string.IsNullOrEmpty(video["url"]?.ToString()) && (drm == null || drm.Type == JTokenType.Null);
        }

        private sealed class Play
        {
            public string PlayId { get; set; }

            public string ProviderId { get; set; }
        }
    }
}
