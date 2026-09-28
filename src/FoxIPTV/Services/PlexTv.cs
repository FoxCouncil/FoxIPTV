// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Classes;
    using Newtonsoft.Json.Linq;

    public class PlexTv : IService
    {
        private const string LoginAddress = "https://clients.plex.tv/api/v2/users/anonymous";

        private const string EpgAddress = "https://epg.provider.plex.tv/";

        private const int GuideDays = 2;

        private const double GuideCacheHours = 6;

        private const int GuideRequestsAtOnce = 12;

        private readonly Login _guideLogin = new Login();

        private readonly Login _playLogin = new Login();

        public string Id => "plex";

        public string Title => "Plex";

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

            var headers = await Signed(_guideLogin).ConfigureAwait(false);
            var playToken = await Token(_playLogin).ConfigureAwait(false);
            var list = JObject.Parse(await Web.GetString(EpgAddress + "lineups/plex/channels", headers).ConfigureAwait(false))["MediaContainer"]?["Channel"] as JArray ?? new JArray();
            var channels = new List<Channel>();
            var gridKeys = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var item in list)
            {
                var id = item["id"]?.ToString();

                if (string.IsNullOrEmpty(id) || item["hidden"]?.Value<bool?>() == true)
                {
                    continue;
                }

                channels.Add(new Channel
                {
                    Id = id,
                    Name = item["title"]?.ToString() ?? id,
                    Group = Title,
                    Logo = Uri.TryCreate(item["thumb"]?.ToString(), UriKind.Absolute, out var logo) ? logo : null,
                    Stream = new Uri($"{EpgAddress}library/parts/{id}/?X-Plex-Token={playToken}")
                });

                gridKeys[id] = item["gridKey"]?.ToString();
            }

            ProgressUpdater?.Item1.Report(100);

            TvCore.LogInfo($"[{Title}] {channels.Count} channels for this connection's region; the Region setting is {ProviderParts.Setting(Data, "Region", "us")}");

            var programmes = await Guide(gridKeys, headers).ConfigureAwait(false);

            return new Tuple<List<Channel>, List<Programme>>(ProviderParts.Numbered(channels), programmes);
        }

        private async Task<List<Programme>> Guide(Dictionary<string, string> gridKeys, Dictionary<string, string> headers)
        {
            var requests = new List<Tuple<string, string>>();

            for (var day = 0; day < GuideDays; day++)
            {
                var date = DateTime.Now.AddDays(day).ToString("yyyy-MM-dd");

                requests.AddRange(gridKeys.Where(x => !string.IsNullOrEmpty(x.Value)).Select(x => Tuple.Create(x.Key, $"{EpgAddress}grid?channelGridKey={x.Value}&date={date}")));
            }

            var pages = new string[requests.Count];
            var done = 0;

            using (var gate = new SemaphoreSlim(GuideRequestsAtOnce))
            {
                await Task.WhenAll(requests.Select(async (request, index) =>
                {
                    await gate.WaitAsync().ConfigureAwait(false);

                    try
                    {
                        pages[index] = await Web.GetStringCached(request.Item2, "plex-" + request.Item2.ToMD5(), GuideCacheHours, headers).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        TvCore.LogDebug($"[{Title}] Guide page failed {request.Item2}: {ex.Message}");
                    }
                    finally
                    {
                        gate.Release();

                        var count = Interlocked.Increment(ref done);

                        if (count % 50 == 0)
                        {
                            ProgressUpdater?.Item2.Report(count * 100 / requests.Count);
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

                foreach (var item in JObject.Parse(pages[index])["MediaContainer"]?["Metadata"] ?? new JArray())
                {
                    foreach (var media in item["Media"] ?? new JArray())
                    {
                        var begins = media["beginsAt"]?.Value<long?>();
                        var ends = media["endsAt"]?.Value<long?>();

                        if (begins.HasValue && ends.HasValue && ends > begins)
                        {
                            programmes.Add(ProviderParts.Programme(requests[index].Item1, DateTimeOffset.FromUnixTimeSeconds(begins.Value), DateTimeOffset.FromUnixTimeSeconds(ends.Value), item["grandparentTitle"]?.ToString() ?? item["title"]?.ToString(), item["summary"]?.ToString()));
                        }
                    }
                }
            }

            return programmes;
        }

        private static async Task<Dictionary<string, string>> Signed(Login login)
        {
            var headers = Headers(login);

            headers["X-Plex-Token"] = await Token(login).ConfigureAwait(false);

            return headers;
        }

        private static async Task<string> Token(Login login)
        {
            if (login.Token == null)
            {
                var answer = await Web.PostString($"{LoginAddress}?X-Plex-Product=Plex%20Web&X-Plex-Client-Identifier={login.ClientId}", string.Empty, "application/json", Headers(login)).ConfigureAwait(false);

                login.Token = JObject.Parse(answer)["authToken"]?.ToString();
            }

            return login.Token;
        }

        private static Dictionary<string, string> Headers(Login login)
        {
            return new Dictionary<string, string>
            {
                ["Accept"] = "application/json",
                ["X-Plex-Product"] = "Plex Web",
                ["X-Plex-Version"] = "4.150.0",
                ["X-Plex-Client-Identifier"] = login.ClientId,
                ["X-Plex-Platform"] = "Web"
            };
        }

        private sealed class Login
        {
            public string ClientId { get; } = Guid.NewGuid().ToString("N");

            public string Token { get; set; }
        }
    }
}
