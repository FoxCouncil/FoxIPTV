// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library.Services
{
    using FoxIPTV.Library.Utils;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;

    public class XtreamService : IService
    {
        private const string ApplicationPath = "player_api.php";
        private const string GuidePath = "xmltv.php";

        private const string AuthenticationFormat = "username={0}&password={1}";

        private const string ChannelQueryParam = "&action=get_live_streams";
        private const string ChannelCategoriesParam = "&action=get_live_categories";

        private const string VideoLiveStreamUrl = "live/{0}/{1}/{2}.ts";

        private const string UrlKey = "Xtream URL";

        public Guid Id => Guid.Parse("2e7c616f-2dc3-462c-80ce-bb01c4988521");

        public string Title => "Xtream IPTV";

        public IReadOnlyList<ServiceField> Fields => new List<ServiceField>
        {
            new ServiceField(Core.UsernameKey, typeof(string)),
            new ServiceField(Core.PasswordKey, typeof(string)),
            new ServiceField(UrlKey, typeof(string), "Server URL", "https://example.org")
        };

        public async Task<Result> IsAuthenticated(JObject data)
        {
            foreach (var field in Fields)
            {
                if (!data.ContainsKey(field.Key))
                {
                    return Result.Failure("Data is malformed");
                }

                if (field.Type == typeof(string))
                {
                    var stringValue = data[field.Key].ToString();

                    if (string.IsNullOrWhiteSpace(stringValue))
                    {
                        return Result.Failure($"Field {field.Key} is empty!");
                    }
                }
            }

            var isValidUrl = Uri.TryCreate(data[UrlKey].ToString(), UriKind.Absolute, out var serviceUrl);

            if (!isValidUrl)
            {
                return Result.Failure($"Field {Fields.First(x => x.Key == UrlKey).Header} is not a valid URL!");
            }

            var username = data[Core.UsernameKey].ToString();
            var password = data[Core.PasswordKey].ToString();

            var authUri = new UriBuilder(serviceUrl)
            {
                Path = ApplicationPath,
                Query = string.Format(AuthenticationFormat, HttpUtility.UrlEncode(username), HttpUtility.UrlEncode(password))
            };

            Authentication result;

            try
            { 
                result = await ApiClient.DeserializeAndMemoryCacheAync<Authentication>(authUri.Uri, TimeSpan.FromMinutes(5));
            }
            catch (HttpRequestException)
            {
                return Result.Failure("Unable to connect to server, check your input.");
            }
            catch (JsonReaderException)
            {
                return Result.Failure("Unable to communicate with the Xtream service, check your input.");
            }

            if (!result.UserInfo.Auth)
            {
                return Result.Failure("Invalid credentials!");
            }
            else
            {
                return Result.Success();
            }
        }

        public async Task<List<Channel>> GetChannels(Account providerAccount)
        {
            if (providerAccount.ProviderId != Id)
            {
                return null;
            }

            var data = providerAccount.Data;

            var isValidUrl = Uri.TryCreate(data[UrlKey].ToString(), UriKind.Absolute, out var serviceUrl);

            var username = data[Core.UsernameKey].ToString();
            var password = data[Core.PasswordKey].ToString();

            var authUri = new UriBuilder(serviceUrl)
            {
                Path = ApplicationPath,
                Query = string.Format(AuthenticationFormat, HttpUtility.UrlEncode(username), HttpUtility.UrlEncode(password)) + ChannelQueryParam
            };

            var channels = await ApiClient.GetStringAndFileCacheAsync(authUri.Uri, TimeSpan.FromHours(1));

            var channelsParse = JsonConvert.DeserializeObject<List<XtreamChannel>>(channels);

            var channelList = new List<Channel>();

            foreach (var providerChannel in channelsParse)
            {
                channelList.Add(new Channel
                {
                    Index = Convert.ToUInt32(providerChannel.Num),
                    Name = providerChannel.Name,
                    CategoryKey = providerChannel.CategoryId,
                    GuideKey = providerChannel.EpgChannelId,
                    Stream = new Uri($"{serviceUrl}{string.Format(VideoLiveStreamUrl, username, password, providerChannel.StreamId)}"),
                    Icon = !string.IsNullOrWhiteSpace(providerChannel.StreamIcon) ? new Uri(providerChannel.StreamIcon) : null
                });
            }

            return channelList;
        }
    }

    public class XtreamChannel
    {
        [JsonProperty("num")]
        public int Num { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("stream_type")]
        public string StreamType { get; set; }

        [JsonProperty("stream_id")]
        public int StreamId { get; set; }

        [JsonProperty("stream_icon")]
        public string StreamIcon { get; set; }

        [JsonProperty("epg_channel_id")]
        public string EpgChannelId { get; set; }

        [JsonProperty("added")]
        public string Added { get; set; }

        [JsonProperty("category_id")]
        public string CategoryId { get; set; }

        [JsonProperty("custom_sid")]
        public string CustomSid { get; set; }

        [JsonProperty("tv_archive")]
        public int TvArchive { get; set; }

        [JsonProperty("direct_source")]
        public string DirectSource { get; set; }

        [JsonProperty("tv_archive_duration")]
        public string TvArchiveDuration { get; set; }
    }

    public class Authentication
    {
        [JsonProperty("user_info")]
        public UserInfo UserInfo { get; set; }

        [JsonProperty("server_info", NullValueHandling = NullValueHandling.Ignore)]
        public ServerInfo ServerInfo { get; set; }
    }

    public class ServerInfo
    {
        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }

        [JsonProperty("port", NullValueHandling = NullValueHandling.Ignore)]
        public string Port { get; set; }

        [JsonProperty("https_port", NullValueHandling = NullValueHandling.Ignore)]
        public string HttpsPort { get; set; }

        [JsonProperty("server_protocol", NullValueHandling = NullValueHandling.Ignore)]
        public string ServerProtocol { get; set; }

        [JsonProperty("rtmp_port", NullValueHandling = NullValueHandling.Ignore)]
        public string RtmpPort { get; set; }

        [JsonProperty("timezone", NullValueHandling = NullValueHandling.Ignore)]
        public string Timezone { get; set; }

        [JsonProperty("timestamp_now", NullValueHandling = NullValueHandling.Ignore)]
        public long? TimestampNow { get; set; }

        [JsonProperty("time_now", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? TimeNow { get; set; }
    }

    public partial class UserInfo
    {
        [JsonProperty("username", NullValueHandling = NullValueHandling.Ignore)]
        public string Username { get; set; }

        [JsonProperty("password", NullValueHandling = NullValueHandling.Ignore)]
        public string Password { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty("auth")]
        public bool Auth { get; set; }

        [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
        public string Status { get; set; }

        [JsonProperty("exp_date", NullValueHandling = NullValueHandling.Ignore)]
        public long? ExpDate { get; set; }

        [JsonProperty("is_trial", NullValueHandling = NullValueHandling.Ignore)]
        public long? IsTrial { get; set; }

        [JsonProperty("active_cons", NullValueHandling = NullValueHandling.Ignore)]
        public long? ActiveCons { get; set; }

        [JsonProperty("created_at", NullValueHandling = NullValueHandling.Ignore)]
        public long? CreatedAt { get; set; }

        [JsonProperty("max_connections", NullValueHandling = NullValueHandling.Ignore)]
        public long? MaxConnections { get; set; }

        [JsonProperty("allowed_output_formats", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> AllowedOutputFormats { get; set; }
    }
}
