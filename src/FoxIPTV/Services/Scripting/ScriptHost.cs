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

    /// <summary>The object a plugin script sees as <c>__host</c>; the prelude wraps it into the friendlier <c>host</c> API</summary>
    /// <remarks>Everything here runs on the script's worker thread while the engine lock is held, so blocking on I/O is fine</remarks>
    public class ScriptHost
    {
        /// <summary>The camelCase JSON settings used when handing .NET objects to scripts</summary>
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter(new CamelCaseNamingStrategy()) }
        };

        private readonly ScriptProvider _provider;

        /// <summary>Create a host bound to a provider</summary>
        /// <param name="provider">The provider whose settings and engine are exposed</param>
        public ScriptHost(ScriptProvider provider)
        {
            _provider = provider;
        }

        /// <summary>The user agent string FoxIPTV sends</summary>
        public string UserAgent => Web.UserAgent;

        /// <summary>Write a line to the FoxIPTV log, tagged with the plugin title</summary>
        /// <param name="message">The message</param>
        public void Log(string message)
        {
            TvCore.LogInfo($"[{_provider.Title}] {message}");
        }

        /// <summary>Read a value the user entered for one of the plugin's fields</summary>
        /// <param name="key">The field key</param>
        /// <returns>The value, the field default, or null</returns>
        public string Setting(string key)
        {
            return _provider.GetSetting(key);
        }

        /// <summary>Download a URL to text</summary>
        /// <param name="url">The absolute URL</param>
        /// <param name="optionsJson">Optional JSON: { headers: {}, cache: hours, cacheKey: "name" }</param>
        /// <returns>The body as text, gzip inflated if needed</returns>
        public string Fetch(string url, string optionsJson)
        {
            var options = ParseOptions(optionsJson, url, out var headers, out var cacheHours, out var cacheKey);

            TvCore.LogDebug($"[{_provider.Title}] fetch {url}");

            return Web.GetStringCached(url, cacheKey, cacheHours, headers).GetAwaiter().GetResult();
        }

        /// <summary>Parse M3U text</summary>
        /// <param name="text">The playlist contents</param>
        /// <returns>The playlist as camelCase JSON: { guideUrls: [], entries: [ { index, id, name, group, logo, url, attributes, options } ] }</returns>
        public string ParseM3U(string text)
        {
            return JsonConvert.SerializeObject(M3UParser.Parse(text), JsonSettings);
        }

        /// <summary>Download and parse an M3U playlist without the text ever entering the script engine</summary>
        /// <param name="url">The playlist URL</param>
        /// <param name="optionsJson">The same options as <see cref="Fetch"/></param>
        /// <returns>The same JSON as <see cref="ParseM3U"/></returns>
        public string FetchM3U(string url, string optionsJson)
        {
            return ParseM3U(Fetch(url, optionsJson));
        }

        /// <summary>Parse XMLTV text into a native programme list the script can hand straight back from guide()</summary>
        /// <param name="text">The XMLTV contents</param>
        /// <param name="progress">An optional JS function taking a percentage</param>
        /// <returns>An opaque native guide object</returns>
        public object ParseXmltv(string text, JsValue progress)
        {
            return XmltvParser.Parse(text, MakeProgress(progress));
        }

        /// <summary>Download and parse an XMLTV guide without the text ever entering the script engine</summary>
        /// <param name="url">The guide URL, .gz is fine</param>
        /// <param name="optionsJson">The same options as <see cref="Fetch"/></param>
        /// <param name="progress">An optional JS function taking a percentage</param>
        /// <returns>An opaque native guide object</returns>
        public object FetchXmltv(string url, string optionsJson, JsValue progress)
        {
            return ParseXmltv(Fetch(url, optionsJson), progress);
        }

        /// <summary>MD5 a string, handy for cache keys</summary>
        /// <param name="value">The input</param>
        /// <returns>A hex digest</returns>
        public string Md5(string value)
        {
            return value.ToMD5();
        }

        /// <summary>Wrap a JS progress function into an <see cref="IProgress{T}"/></summary>
        /// <param name="progress">A JS function or null</param>
        /// <returns>A reporter, or null</returns>
        private IProgress<int> MakeProgress(JsValue progress)
        {
            if (progress == null || progress.IsNull() || progress.IsUndefined())
            {
                return null;
            }

            return new SynchronousProgress(percent => _provider.InvokeCallback(progress, percent));
        }

        /// <summary>Parse the fetch options JSON</summary>
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

        /// <summary>An <see cref="IProgress{T}"/> that calls back on the reporting thread, which is what a script expects</summary>
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
