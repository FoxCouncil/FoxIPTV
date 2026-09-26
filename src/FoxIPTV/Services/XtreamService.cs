// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using Classes;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;

    /// <summary>An Xtream Codes compatible IPTV panel, the de facto commercial IPTV API</summary>
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

        /// <inheritdoc/>
        public string Id => "xtream";

        /// <inheritdoc/>
        public string Title { get; } = "Xtream IPTV";

        /// <inheritdoc/>
        public string Description => "Any Xtream Codes compatible provider; needs the panel URL and your account";

        /// <inheritdoc/>
        public ProviderCapabilities Capabilities => ProviderCapabilities.LiveTv;

        /// <inheritdoc/>
        public List<ProviderField> Fields { get; } = new List<ProviderField>
        {
            ProviderField.Text(UsernameKey),
            ProviderField.Password(PasswordKey),
            ProviderField.Url(ServicesKey)
        };

        /// <inheritdoc/>
        public JObject Data { get; set; }

        /// <inheritdoc/>
        public bool SaveAuthentication { get; set; }

        /// <inheritdoc/>
        public Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        /// <inheritdoc/>
        public async Task<bool> IsAuthenticated()
        {
            TvCore.LogDebug($"[{Title}] IsAuthenticated() called...");

            var authDataFile = Path.Combine(TvCore.UserStoragePath, ServiceDataFilename);

            Uri domainUri = null;

            Uri authUrl;

            var haveCredentials = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(Services);

            if (haveCredentials || !File.Exists(authDataFile))
            {
                if (!haveCredentials)
                {
                    TvCore.LogError($"[{Title}] IsAuthenticated(): Username/Password/ServiceURL is empty");

                    return false;
                }

                if (!Uri.IsWellFormedUriString(Services, UriKind.Absolute))
                {
                    TvCore.LogError($"[{Title}] IsAuthenticated(): ServiceURL is not a well formed link");

                    return false;
                }

                domainUri = new Uri(Services);

                authUrl = new Uri($"{domainUri.Scheme}://{domainUri.DnsSafeHost}:{domainUri.Port}/{string.Format(AuthenticationUrl, HttpUtility.UrlEncode(Username), HttpUtility.UrlEncode(Password))}");
            }
            else
            {
                try
                {
                    _authData = JsonConvert.DeserializeObject<AuthResponse>(File.ReadAllText(authDataFile).Unprotect());
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[{Title}] IsAuthenticated(): Saved authentication unreadable, {ex.Message}");

                    return false;
                }

                authUrl = new Uri(BuildUri(AuthenticationUrl));
            }

            TvCore.LogDebug($"[{Title}] IsAuthenticated(): Checking server authentication...");

            try
            {
                var data = await Web.GetString(authUrl.ToString());

                _authData = JsonConvert.DeserializeObject<AuthResponse>(data);

                if (_authData?.Server == null || _authData.User == null)
                {
                    TvCore.LogError($"[{Title}] IsAuthenticated(): Server returned an unexpected response");

                    return false;
                }

                if (domainUri != null)
                {
                    _authData.Server.UserUrl = domainUri.DnsSafeHost;
                    _authData.Server.UserPort = domainUri.Port;
                    _authData.Server.UserProtocol = domainUri.Scheme;
                }

                TvCore.LogDebug($"[{Title}] IsAuthenticated(): Checking server connection SUCCESS! IsAuthenticated: {_authData.User.Authenticated}");
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[{Title}] IsAuthenticated(): Checking server authentication ERROR: {ex.Message}");

                return false;
            }

            if (_authData.User.Authenticated != 1)
            {
                return false;
            }

            TvCore.LogDebug($"[{Title}] IsAuthenticated(): Server authentication SUCCESS! Server address: {_authData.Server.Url}");

            if (SaveAuthentication)
            {
                TvCore.LogDebug($"[{Title}] IsAuthenticated(): Saving auth data...");

                File.WriteAllText(authDataFile, JsonConvert.SerializeObject(_authData).Protect());
            }

            TvCore.LogInfo($"[{Title}] Log-in successful");

            return true;
        }

        private string BuildUri(string endpointUrl)
        {
            var url = string.IsNullOrEmpty(_authData.Server.UserUrl) ? _authData.Server.Url : _authData.Server.UserUrl;
            var port = _authData.Server.UserPort == 0 ? _authData.Server.Port : _authData.Server.UserPort.ToString();
            var protocol = string.IsNullOrEmpty(_authData.Server.UserProtocol) ? _authData.Server.ServerProtocol : _authData.Server.UserProtocol;

            var user = HttpUtility.UrlEncode(_authData.User.Username);
            var password = HttpUtility.UrlEncode(_authData.User.Password);

            return $"{protocol}://{url}:{port}/{string.Format(endpointUrl, user, password)}";
        }

        /// <inheritdoc/>
        public async Task<Tuple<List<Channel>, List<Programme>>> Process()
        {
            if (_authData == null)
            {
                return new Tuple<List<Channel>, List<Programme>>(new List<Channel>(), new List<Programme>());
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

            var categoryDictionary = JArray.Parse(categoryRaw).ToDictionary(k => k["category_id"]?.ToString() ?? string.Empty, v => v["category_name"]?.ToString() ?? string.Empty);

            progressPercentage.Report(30);

            var channelRaw = await TvCore.DownloadStringAndCache(BuildUri(ChannelUrl), ChannelCacheFilename, CacheTimeChannelsInHours);

            var channelArray = JArray.Parse(channelRaw);

            progressPercentage.Report(60);

            var channelList = new List<Channel>();

            foreach (var chan in channelArray)
            {
                try
                {
                    var group = chan["category_id"]?.ToString() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(group) && categoryDictionary.ContainsKey(group))
                    {
                        group = categoryDictionary[group];
                    }

                    channelList.Add(new Channel
                    {
                        Index = chan["num"].ToObject<uint>(),
                        Id = chan["epg_channel_id"]?.ToString() ?? string.Empty,
                        Name = chan["name"]?.ToString() ?? string.Empty,
                        Logo = Uri.TryCreate(chan["stream_icon"]?.ToString(), UriKind.Absolute, out var result) ? result : null,
                        Group = group,
                        Stream = new Uri($"{BuildUri(VideoLiveStreamUrl)}{chan["stream_id"]}.ts")
                    });
                }
                catch (Exception e)
                {
                    TvCore.LogError($"[{Title}] ProcessChannels(): Skipping malformed channel, {e.Message}");
                }
            }

            progressPercentage.Report(100);

            TvCore.LogDebug($"[{Title}] ProcessChannels() End: {channelList.Count} channel(s) processed");

            return channelList;
        }

        private async Task<List<Programme>> ProcessGuide()
        {
            TvCore.LogDebug($"[{Title}] ProcessGuide() Start...");

            var progressPercentage = ProgressUpdater.Item2;

            progressPercentage.Report(0);

            var guideRaw = await TvCore.DownloadStringAndCache(BuildUri(GuideUrl), GuideCacheFilename, CacheTimeGuideInHours);

            var guideList = await Task.Run(() => XmltvParser.Parse(guideRaw, progressPercentage));

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
            public long? ExpirationTimestamp { get; set; }

            [JsonProperty("is_trial")]
            public string IsTrial { get; set; }

            [JsonProperty("active_cons")]
            public string ActiveConnections { get; set; }

            [JsonProperty("created_at")]
            public string CreatedAt { get; set; }

            [JsonProperty("max_connections")]
            public string MaxConnection { get; set; }

            [JsonProperty("allowed_output_formats")]
            public List<string> AllowedOutputFormats { get; set; }
        }

        private class ServerInfo
        {
            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("user_url")]
            public string UserUrl { get; set; }

            [JsonProperty("user_protocol")]
            public string UserProtocol { get; set; }

            [JsonProperty("port")]
            public string Port { get; set; }

            [JsonProperty("user_port")]
            public int UserPort { get; set; }

            [JsonProperty("https_port")]
            public string HttpsPort { get; set; }

            [JsonProperty("server_protocol")]
            public string ServerProtocol { get; set; }

            [JsonProperty("rtmp_port")]
            public string RtmpPort { get; set; }

            [JsonProperty("timezone")]
            public string Timezone { get; set; }

            [JsonProperty("timestamp_now")]
            public long? TimestampNow { get; set; }

            [JsonProperty("time_now")]
            public string TimeNow { get; set; }
        }
    }
}
