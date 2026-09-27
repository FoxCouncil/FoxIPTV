// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services.Scripting
{
    using Classes;
    using Jint;
    using Jint.Native;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;
    using System;
    using System.Collections.Generic;

    public class ScriptHost
    {
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter(new CamelCaseNamingStrategy()) }
        };

        private readonly ScriptProvider _provider;

        public ScriptHost(ScriptProvider provider)
        {
            _provider = provider;
        }

        public string UserAgent => Web.UserAgent;

        public void Log(string message)
        {
            TvCore.LogInfo($"[{_provider.Title}] {message}");
        }

        public string Setting(string key)
        {
            return _provider.GetSetting(key);
        }

        public string Fetch(string url, string optionsJson)
        {
            var options = ParseOptions(optionsJson, url, out var headers, out var cacheHours, out var cacheKey);

            TvCore.LogDebug($"[{_provider.Title}] fetch {url}");

            return Web.GetStringCached(url, cacheKey, cacheHours, headers).GetAwaiter().GetResult();
        }

        public string ParseM3U(string text)
        {
            return JsonConvert.SerializeObject(M3UParser.Parse(text), JsonSettings);
        }

        public string FetchM3U(string url, string optionsJson)
        {
            return ParseM3U(Fetch(url, optionsJson));
        }

        public object ParseXmltv(string text, JsValue progress)
        {
            return XmltvParser.Parse(text, MakeProgress(progress));
        }

        public object FetchXmltv(string url, string optionsJson, JsValue progress)
        {
            return ParseXmltv(Fetch(url, optionsJson), progress);
        }

        public string Md5(string value)
        {
            return value.ToMD5();
        }

        private IProgress<int> MakeProgress(JsValue progress)
        {
            if (progress == null || progress.IsNull() || progress.IsUndefined())
            {
                return null;
            }

            return new SynchronousProgress(percent => _provider.InvokeCallback(progress, percent));
        }

        private static JObject ParseOptions(string optionsJson, string url, out Dictionary<string, string> headers, out double cacheHours, out string cacheKey)
        {
            headers = null;
            cacheHours = 0;
            cacheKey = null;

            if (string.IsNullOrWhiteSpace(optionsJson))
            {
                return null;
            }

            var options = JObject.Parse(optionsJson);

            if (options["headers"] is JObject headerObject)
            {
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var header in headerObject)
                {
                    headers[header.Key] = header.Value?.ToString();
                }
            }

            var cacheToken = options["cache"];

            if (cacheToken != null && cacheToken.Type != JTokenType.Null)
            {
                cacheHours = cacheToken.Type == JTokenType.Boolean ? (cacheToken.Value<bool>() ? 6 : 0) : cacheToken.Value<double>();
            }

            cacheKey = options["cacheKey"]?.ToString();

            if (cacheHours > 0 && string.IsNullOrWhiteSpace(cacheKey))
            {
                cacheKey = "js-" + url.ToMD5();
            }

            return options;
        }

        private class SynchronousProgress : IProgress<int>
        {
            private readonly Action<int> _handler;

            public SynchronousProgress(Action<int> handler)
            {
                _handler = handler;
            }

            public void Report(int value)
            {
                _handler(value);
            }
        }
    }
}
