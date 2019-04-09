// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using Classes;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    public class XtreamService : IService
    {
        private const int CacheTimeChannelsInHours = 12;
        private const int CacheTimeGuideInHours = 6;

        private const string AuthenticationUrl = "player_api.php?username={0}&password={1}";
        private const string GuideUrl = "xmltv.php?username={0}&password={1}";
        private const string ChannelUrl = "player_api.php?username={0}&password={1}&action=get_live_streams";
        private const string ChannelCategoriesUrl = "player_api.php?username={0}&password={1}&action=get_live_categories";

        private const string VideoLiveStreamUrl = "live/{0}/{1}/";

        private const string ChannelCacheFilename = "cdata";
        private const string ChannelCategoryCacheFilename = "ccdata";
        private const string GuideCacheFilename = "gdata";
        private const string ServiceDataFilename = "sdata";

        private const string UsernameKey = "Username";
        private const string PasswordKey = "Password";
        private const string ServicesKey = "Xtream URL";

        private AuthResponse _authData;

        private string Username => Data?[UsernameKey]?.ToString();

        private string Password => Data?[PasswordKey]?.ToString();

        private string Services => Data?[ServicesKey]?.ToString();

        public string Title { get; } = "Xtream IPTV";

        public Dictionary<string, Type> Fields { get; } = new Dictionary<string, Type>
        {
            {UsernameKey, typeof(string)},
            {PasswordKey, typeof(string)},
            {ServicesKey, typeof(string)}
        };

        public JObject Data { get; set; }

        public bool SaveAuthentication { get; set; }

        public async Task<bool> IsAuthenticated()
        {
            TvCore.LogDebug($"[{Title}] IsAuthenticated() called...");

            var authDataFile = Path.Combine(TvCore.UserStoragePath, ServiceDataFilename);

            Uri authUrl;

            if (!File.Exists(authDataFile))
            {
                TvCore.LogDebug($"[{Title}] IsAuthenticated(): No authentication file found...");

                if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Services))
                {
                    TvCore.LogError($"[{Title}] IsAuthenticated(): Username/Password/ServiceURL is empty");

                    return false;
                }

                if (!Uri.IsWellFormedUriString(Services, UriKind.Absolute))
                {
                    TvCore.LogError($"[{Title}] IsAuthenticated(): ServiceURL is not a well formed link");

                    return false;
                }

                var domainUri = new Uri(Services);

                authUrl = new Uri($"http://{domainUri.DnsSafeHost}/{string.Format(AuthenticationUrl, Username, Password)}");

                SaveAuthentication = true;
            }
            else
            {
                _authData = JsonConvert.DeserializeObject<AuthResponse>(File.ReadAllText(authDataFile).Unprotect());

                authUrl = new Uri(BuildUri(AuthenticationUrl));
            }

            TvCore.LogDebug($"[{Title}] IsAuthenticated(): Checking server authentication...");

            using (var wc = new WebClient())
            {
                wc.Headers.Add("user-agent", TvCore.ServiceUserAgentString);

                try
                {
                    var data = await wc.DownloadStringTaskAsync(authUrl);
                    _authData = JsonConvert.DeserializeObject<AuthResponse>(data);

                    TvCore.LogDebug($"[{Title}] IsAuthenticated(): Checking server connection SUCCESS! IsAuthenticated: {_authData.User.Authenticated}");
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[{Title}] IsAuthenticated(): Checking server authentication ERROR: {ex.Message}");

                    return false;
                }
            }

            if (_authData == null)
            {
                return false;
            }

            if (_authData.User.Authenticated != 1)
            {
                return false;
            }

            TvCore.LogDebug($"[{Title}] IsAuthenticated(): Server authentication SUCCESS! Server address: {_authData.Server.Url}");

            if (!SaveAuthentication)
            {
                TvCore.LogDebug($"[{Title}] IsAuthenticated(): Not Saving User Authentication...");

                TvCore.LogInfo($"[{Title}] Log-in successful");

                return true;
            }

            TvCore.LogDebug($"[{Title}] IsAuthenticated(): Saving auth data...");

            File.WriteAllText(authDataFile, JsonConvert.SerializeObject(_authData).Protect());

            TvCore.LogInfo($"[{Title}] Log-in successful");

            return true;
        }

        private string BuildUri(string endpointUrl)
        {
            return $"{_authData.Server.ServerProtocol}://{_authData.Server.Url}:{_authData.Server.Port}/{string.Format(endpointUrl, _authData.User.Username, _authData.User.Password)}";
        }

        public Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        public async Task<Tuple<List<Channel>, List<Programme>>> Process()
        {
            if (_authData == null && Data == null)
            {
                return null;
            }

            TvCore.LogDebug($"[{Title}] Process(): Starting data processing...");

            var item1 = await ProcessChannels();
            var item2 = await ProcessGuide();

            return new Tuple<List<Channel>, List<Programme>>(item1, item2);
        }

        private async Task<List<Channel>> ProcessChannels()
        {
            TvCore.LogDebug($"[{Title}] ProcessChannels() Start...");

            var progressPercentage = ProgressUpdater.Item1;

            progressPercentage.Report(0);

            var categoryRaw = await TvCore.DownloadStringAndCache(BuildUri(ChannelCategoriesUrl), ChannelCategoryCacheFilename, CacheTimeChannelsInHours);

            var categoryDictionary = JArray.Parse(categoryRaw).ToDictionary(k => k["category_id"].ToString(), v => v["category_name"].ToString());

            var channelRaw = await TvCore.DownloadStringAndCache(BuildUri(ChannelUrl), ChannelCacheFilename, CacheTimeChannelsInHours);

            var channelArray = JArray.Parse(channelRaw);

            var channelList = new List<Channel>();

            foreach (var chan in channelArray)
            {
                try
                {
                    var group = chan["category_id"].ToString();
                    if (!string.IsNullOrWhiteSpace(group) && categoryDictionary.ContainsKey(group))
                    {
                        group = categoryDictionary[group];
                    }

                    channelList.Add(new Channel
                    {
                        Index = chan["num"].ToObject<uint>(),
                        Id = chan["epg_channel_id"].ToString(),
                        Name = chan["name"].ToString(),
                        Logo = Uri.TryCreate(chan["stream_icon"].ToString(), UriKind.Absolute, out var result) ? result : null,
                        Group = group,
                        Stream = new Uri($"{BuildUri(VideoLiveStreamUrl)}{chan["stream_id"]}.ts")
                    });
                }
                catch (Exception e)
                {
                    Debugger.Break();
                    Console.WriteLine(e);
                    throw;
                }
            }

            TvCore.LogDebug($"[{Title}] ProcessChannels() End: {channelList.Count} channel(s) processed");

            return channelList;
        }

        private async Task<List<Programme>> ProcessGuide()
        {
            TvCore.LogDebug($"[{Title}] ProcessGuide() Start...");

            var progressPercentage = ProgressUpdater.Item2;

            progressPercentage.Report(0);

            var guideRaw = await TvCore.DownloadStringAndCache(BuildUri(GuideUrl), GuideCacheFilename, CacheTimeGuideInHours);

            var guideList = new List<Programme>();
            var parsedGuide = XDocument.Parse(guideRaw);

            var totals = parsedGuide.Descendants("programme").Count();
            var idx = 0;

            foreach (var element in parsedGuide.Descendants("programme"))
            {
                DateTimeOffset start;
                DateTimeOffset stop;

                try
                {
                    start = DateTimeOffset.ParseExact(element.Attribute("start")?.Value, "yyyyMMddHHmmss zzz", null);
                    stop = DateTimeOffset.ParseExact(element.Attribute("stop")?.Value, "yyyyMMddHHmmss zzz", null);
                }
                catch (Exception)
                {
                    continue;
                }

                var newProgramme = new Programme
                {
                    Channel = element.Attribute("channel")?.Value,
                    Title = element.Element("title")?.Value,
                    Description = element.Element("desc")?.Value,
                    Start = start,
                    Stop = stop,
                    BlockLength = (int)Math.Floor((stop - start).TotalMinutes / 10d)
                };

                guideList.Add(newProgramme);

                var percentage = (int)(idx++ / (float)totals * 100.0f);
                progressPercentage.Report(percentage);
            }

            TvCore.LogDebug($"[{Title}] ProcessGuide() End: {guideList.Count} guide programme(s) processed");

            return guideList;
        }

        private class AuthResponse
        {
            [JsonProperty("user_info")]
            public UserInfo User { get; set; }

            [JsonProperty("server_info")]
            public ServerInfo Server { get; set; }
        }

        private class UserInfo
        {
            [JsonProperty("username")]
            public string Username { get; set; }

            [JsonProperty("password")]
            public string Password { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("auth")]
            public int Authenticated { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("exp_date")]
            public int ExpirationTimestamp { get; set; }

            [JsonProperty("is_trial")]
            public int IsTrial { get; set; }

            [JsonProperty("active_cons")]
            public int ActiveConnections { get; set; }

            [JsonProperty("created_at")]
            public int CreatedAt { get; set; }

            [JsonProperty("max_connections")]
            public int MaxConnection { get; set; }

            [JsonProperty("allowed_output_formats")]
            public List<string> AllowedOutputFormats { get; set; }
        }

        private class ServerInfo
        {
            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("port")]
            public int Port { get; set; }

            [JsonProperty("https_port")]
            public int HttpsPort { get; set; }

            [JsonProperty("server_protocol")]
            public string ServerProtocol { get; set; }

            [JsonProperty("rtmp_port")]
            public int RtmpPort { get; set; }

            [JsonProperty("timezone")]
            public string Timezone { get; set; }

            [JsonProperty("timestamp_now")]
            public int TimestampNow { get; set; }

            [JsonProperty("time_now")]
            public string TimeNow { get; set; }
        }
    }
}
