// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services.Scripting
{
    using Classes;
    using Jint;
    using Jint.Native;
    using Jint.Native.Object;
    using Jint.Runtime;
    using Jint.Runtime.Interop;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    public class ScriptProvider : IService, ILibraryProvider
    {
        private const string Prelude = @"
var host = (function () {
    function opts(o) { return o ? JSON.stringify(o) : null; }
    return {
        userAgent: __host.UserAgent,
        log: function (m) { __host.Log(String(m)); },
        fetch: function (u, o) { return __host.Fetch(String(u), opts(o)); },
        fetchJson: function (u, o) { return JSON.parse(__host.Fetch(String(u), opts(o))); },
        setting: function (k, d) { var v = __host.Setting(String(k)); return (v === null || v === undefined || v === '') ? d : v; },
        md5: function (s) { return __host.Md5(String(s)); },
        m3u: {
            parse: function (t) { return JSON.parse(__host.ParseM3U(String(t))); },
            fetch: function (u, o) { return JSON.parse(__host.FetchM3U(String(u), opts(o))); }
        },
        xmltv: {
            parse: function (t, p) { return __host.ParseXmltv(String(t), p || null); },
            fetch: function (u, o, p) { return __host.FetchXmltv(String(u), opts(o), p || null); }
        }
    };
})();
";

        private readonly Engine _engine;

        private readonly object _lock = new object();

        private readonly JsValue _jsonParse;

        private readonly JsValue _jsonStringify;

        private readonly HashSet<string> _functions = new HashSet<string>(StringComparer.Ordinal);

        public string Origin { get; }

        public bool IsBuiltIn { get; }

        public string Version { get; private set; } = "1";

        public string Id { get; private set; }

        public string Title { get; private set; }

        public string Description { get; private set; }

        public ProviderCapabilities Capabilities { get; private set; }

        public List<ProviderField> Fields { get; } = new List<ProviderField>();

        public JObject Data { get; set; }

        public bool SaveAuthentication { get; set; }

        public Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        public ScriptProvider(string source, string origin, bool isBuiltIn)
        {
            Origin = origin;
            IsBuiltIn = isBuiltIn;

            _engine = new Engine(options =>
            {
                options.LimitRecursion(512);
                options.CatchClrExceptions();
                options.Interop.AllowGetType = false;
            });

            try
            {
                _engine.SetValue("__host", new ScriptHost(this));
                _engine.Execute(Prelude);

                _jsonParse = _engine.Evaluate("JSON.parse");
                _jsonStringify = _engine.Evaluate("JSON.stringify");

                _engine.Execute(source);

                ReadMetadata();

                foreach (var name in new[] { "authenticate", "channels", "guide", "categories", "browse", "search", "details", "episodes", "resolve" })
                {
                    if (_engine.Evaluate($"typeof {name} === 'function'").AsBoolean())
                    {
                        _functions.Add(name);
                    }
                }
            }
            catch (ScriptException)
            {
                throw;
            }
            catch (JavaScriptException ex)
            {
                throw new ScriptException($"{origin}: {DescribeError(ex)}", ex);
            }
            catch (Exception ex)
            {
                throw new ScriptException($"{origin}: {ex.Message}", ex);
            }
        }

        private void ReadMetadata()
        {
            var plugin = _engine.GetValue("plugin");

            if (!plugin.IsObject())
            {
                throw new ScriptException($"{Origin}: the script must declare a global 'plugin' object");
            }

            Id = Str(plugin, "id");
            Title = Str(plugin, "title") ?? Id;
            Description = Str(plugin, "description") ?? string.Empty;
            Version = Str(plugin, "version") ?? "1";

            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new ScriptException($"{Origin}: plugin.id is required");
            }

            Id = Web.SafeFilename(Id.Trim().ToLowerInvariant());

            var capabilities = ProviderCapabilities.None;

            foreach (var capability in Items(Prop(plugin, "capabilities")))
            {
                var text = capability.ToString().Trim().ToLowerInvariant();

                if (text == "live" || text == "livetv" || text == "channels")
                {
                    capabilities |= ProviderCapabilities.LiveTv;
                }
                else if (text == "library" || text == "vod")
                {
                    capabilities |= ProviderCapabilities.Library;
                }
            }

            Capabilities = capabilities;

            var fields = Prop(plugin, "fields");

            if (fields.IsArray())
            {
                foreach (var field in Items(fields))
                {
                    if (field.IsString())
                    {
                        Fields.Add(ProviderField.Text(field.AsString()));

                        continue;
                    }

                    if (!field.IsObject())
                    {
                        continue;
                    }

                    var key = Str(field, "key") ?? Str(field, "name");

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    var kindText = (Str(field, "kind") ?? Str(field, "type") ?? "text").ToLowerInvariant();

                    var kind = ProviderFieldKind.Text;

                    if (kindText == "password")
                    {
                        kind = ProviderFieldKind.Password;
                    }
                    else if (kindText == "url")
                    {
                        kind = ProviderFieldKind.Url;
                    }
                    else if (kindText == "choice" || kindText == "select")
                    {
                        kind = ProviderFieldKind.Choice;
                    }

                    var required = Prop(field, "required");

                    Fields.Add(new ProviderField
                    {
                        Key = key,
                        Label = Str(field, "label"),
                        Kind = kind,
                        Choices = Items(Prop(field, "choices")).Select(x => x.ToString()).ToList(),
                        Default = Str(field, "default"),
                        Required = required.IsBoolean() ? required.AsBoolean() : true
                    });
                }
            }
            else if (fields.IsObject())
            {
                foreach (var key in fields.AsObject().GetOwnPropertyKeys())
                {
                    Fields.Add(ProviderField.Text(key.ToString()));
                }
            }
        }

        public string GetSetting(string key)
        {
            var value = Data?[key]?.ToString();

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return Fields.FirstOrDefault(x => x.Key == key)?.Default;
        }

        internal void InvokeCallback(JsValue function, object argument)
        {
            _engine.Invoke(function, argument);
        }

        public Task<bool> IsAuthenticated()
        {
            foreach (var field in Fields.Where(x => x.Required))
            {
                if (string.IsNullOrWhiteSpace(GetSetting(field.Key)))
                {
                    TvCore.LogError($"[{Title}] Missing required field '{field.Key}'");

                    return Task.FromResult(false);
                }
            }

            if (!_functions.Contains("authenticate"))
            {
                return Task.FromResult(true);
            }

            return Run(() =>
            {
                var result = _engine.Invoke("authenticate", ToJs(Data ?? new JObject()));

                return TypeConverter.ToBoolean(result);
            });
        }

        public Task<Tuple<List<Channel>, List<Programme>>> Process()
        {
            return Run(() =>
            {
                var channels = new List<Channel>();
                var guide = new List<Programme>();

                var channelProgress = ProgressUpdater?.Item1;
                var guideProgress = ProgressUpdater?.Item2;

                if (_functions.Contains("channels"))
                {
                    TvCore.LogDebug($"[{Title}] channels() start");

                    channelProgress?.Report(0);

                    var result = _engine.Invoke("channels", new Action<double>(p => channelProgress?.Report(ClampPercent(p))));

                    channels = MapChannels(result);

                    TvCore.LogDebug($"[{Title}] channels() end: {channels.Count} channel(s)");
                }

                channelProgress?.Report(100);

                if (_functions.Contains("guide"))
                {
                    TvCore.LogDebug($"[{Title}] guide() start");

                    guideProgress?.Report(0);

                    var result = _engine.Invoke("guide", new Action<double>(p => guideProgress?.Report(ClampPercent(p))));

                    guide = MapProgrammes(result);

                    TvCore.LogDebug($"[{Title}] guide() end: {guide.Count} programme(s)");
                }

                guideProgress?.Report(100);

                return new Tuple<List<Channel>, List<Programme>>(channels, guide);
            });
        }

        public Task<List<LibraryCategory>> GetCategories()
        {
            return Run(() => Items(Call("categories")).Where(x => x.IsObject()).Select(x => new LibraryCategory { Id = Str(x, "id"), Name = Str(x, "name") ?? Str(x, "id") }).Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToList());
        }

        public Task<LibraryPage> Browse(string categoryId, int page)
        {
            return Run(() => MapPage(Call("browse", categoryId, page), page));
        }

        public Task<LibraryPage> Search(string query, int page)
        {
            return Run(() => MapPage(Call("search", query, page), page));
        }

        public Task<LibraryItem> GetDetails(string id, LibraryItemKind kind)
        {
            return Run(() =>
            {
                if (!_functions.Contains("details"))
                {
                    return null;
                }

                return MapItem(Call("details", id, KindName(kind)));
            });
        }

        public Task<List<LibraryItem>> GetEpisodes(string seriesId, int season)
        {
            return Run(() => Items(Call("episodes", seriesId, season)).Select(MapItem).Where(x => x != null).ToList());
        }

        public Task<List<MediaSource>> Resolve(LibraryItem item)
        {
            return Run(() =>
            {
                var result = Call("resolve", ToJs(JObject.FromObject(item, JsonSerializer.Create(ScriptHost.JsonSettings))));

                var sources = new List<MediaSource>();

                foreach (var source in Items(result))
                {
                    if (source.IsString())
                    {
                        if (Uri.TryCreate(source.AsString(), UriKind.Absolute, out var plainUri))
                        {
                            sources.Add(new MediaSource { Name = $"Source {sources.Count + 1}", Url = plainUri });
                        }

                        continue;
                    }

                    if (!source.IsObject())
                    {
                        continue;
                    }

                    if (!Uri.TryCreate(Str(source, "url"), UriKind.Absolute, out var uri))
                    {
                        continue;
                    }

                    if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "rtsp" && uri.Scheme != "rtmp" && uri.Scheme != "udp" && uri.Scheme != "rtp" && uri.Scheme != "mms" && uri.Scheme != "file")
                    {
                        TvCore.LogError($"[{Title}] resolve() returned a non-stream URL, skipped: {uri}");

                        continue;
                    }

                    var mediaSource = new MediaSource
                    {
                        Name = Str(source, "name") ?? $"Source {sources.Count + 1}",
                        Url = uri
                    };

                    var headers = Prop(source, "headers");

                    if (headers.IsObject())
                    {
                        var headerObject = headers.AsObject();

                        foreach (var key in headerObject.GetOwnPropertyKeys())
                        {
                            mediaSource.Headers[key.ToString()] = headerObject.Get(key).ToString();
                        }
                    }

                    sources.Add(mediaSource);
                }

                return sources;
            });
        }

        private Task<T> Run<T>(Func<T> work)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        return work();
                    }
                    catch (ScriptException)
                    {
                        throw;
                    }
                    catch (JavaScriptException ex)
                    {
                        TvCore.LogError($"[{Title}] {DescribeError(ex)}");

                        throw new ScriptException($"{Title}: {DescribeError(ex)}", ex);
                    }
                    catch (Exception ex)
                    {
                        TvCore.LogError($"[{Title}] {ex.GetType().Name}: {ex.Message}");

                        throw new ScriptException($"{Title}: {ex.Message}", ex);
                    }
                }
            });
        }

        private JsValue Call(string name, params object[] args)
        {
            if (!_functions.Contains(name))
            {
                return JsValue.Undefined;
            }

            return _engine.Invoke(name, args);
        }

        private JsValue ToJs(JToken token)
        {
            return _engine.Invoke(_jsonParse, token.ToString(Formatting.None));
        }

        private JToken ToJson(JsValue value)
        {
            if (value.IsUndefined() || value.IsNull())
            {
                return null;
            }

            var text = _engine.Invoke(_jsonStringify, value);

            return text.IsString() ? JToken.Parse(text.AsString()) : null;
        }

        private static string DescribeError(JavaScriptException ex)
        {
            var location = ex.Location;

            return location.Start.Line > 0 ? $"{ex.Message} (line {location.Start.Line}, column {location.Start.Column})" : ex.Message;
        }

        private static int ClampPercent(double value)
        {
            if (double.IsNaN(value))
            {
                return 0;
            }

            return (int)Math.Max(0, Math.Min(100, value));
        }

        private static string KindName(LibraryItemKind kind)
        {
            switch (kind)
            {
                case LibraryItemKind.Series:
                {
                    return "series";
                }
                case LibraryItemKind.Episode:
                {
                    return "episode";
                }
                default:
                {
                    return "movie";
                }
            }
        }

        private static LibraryItemKind ParseKind(string text)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "series":
                case "tv":
                case "show":
                {
                    return LibraryItemKind.Series;
                }
                case "episode":
                {
                    return LibraryItemKind.Episode;
                }
                default:
                {
                    return LibraryItemKind.Movie;
                }
            }
        }

        private List<Channel> MapChannels(JsValue result)
        {
            var channels = new List<Channel>();

            var usedIndexes = new HashSet<uint>();
            var nextIndex = 1u;

            foreach (var entry in Items(result))
            {
                if (!entry.IsObject())
                {
                    continue;
                }

                var streamText = Str(entry, "stream") ?? Str(entry, "url");

                if (!Uri.TryCreate(streamText, UriKind.Absolute, out var streamUri))
                {
                    continue;
                }

                var index = (uint)Math.Max(0, Int(entry, "index") ?? Int(entry, "number") ?? 0);

                if (index == 0 || usedIndexes.Contains(index))
                {
                    while (usedIndexes.Contains(nextIndex))
                    {
                        nextIndex++;
                    }

                    index = nextIndex;
                }

                usedIndexes.Add(index);

                var name = Str(entry, "name") ?? streamUri.Host;

                channels.Add(new Channel
                {
                    Index = index,
                    Id = Str(entry, "id") ?? string.Empty,
                    Name = name,
                    Group = Str(entry, "group") ?? "Uncategorized",
                    Logo = Uri.TryCreate(Str(entry, "logo"), UriKind.Absolute, out var logo) ? logo : null,
                    Stream = streamUri
                });
            }

            return channels.OrderBy(x => x.Index).ToList();
        }

        private static List<Programme> MapProgrammes(JsValue result)
        {
            if (result is ObjectWrapper wrapper && wrapper.Target is List<Programme> native)
            {
                return native;
            }

            var programmes = new List<Programme>();

            foreach (var entry in Items(result))
            {
                if (!entry.IsObject())
                {
                    continue;
                }

                if (!TryTime(Prop(entry, "start"), out var start) || !TryTime(Prop(entry, "stop"), out var stop) || stop <= start)
                {
                    continue;
                }

                programmes.Add(new Programme
                {
                    Channel = Str(entry, "channel") ?? string.Empty,
                    Title = Str(entry, "title") ?? string.Empty,
                    Description = Str(entry, "description") ?? Str(entry, "desc") ?? string.Empty,
                    Start = start,
                    Stop = stop,
                    BlockLength = (int)Math.Floor((stop - start).TotalMinutes / 10d)
                });
            }

            return programmes;
        }

        private LibraryPage MapPage(JsValue result, int requestedPage)
        {
            var page = new LibraryPage { Page = requestedPage };

            if (result.IsArray())
            {
                page.Items = Items(result).Select(MapItem).Where(x => x != null).ToList();

                return page;
            }

            if (!result.IsObject())
            {
                return page;
            }

            page.Items = Items(Prop(result, "items")).Select(MapItem).Where(x => x != null).ToList();
            page.Page = Int(result, "page") ?? requestedPage;
            page.TotalPages = Math.Max(1, Int(result, "totalPages") ?? Int(result, "pages") ?? 1);

            return page;
        }

        private LibraryItem MapItem(JsValue value)
        {
            if (!value.IsObject())
            {
                return null;
            }

            var id = Str(value, "id");

            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var item = new LibraryItem
            {
                Id = id,
                Kind = ParseKind(Str(value, "kind") ?? Str(value, "type")),
                Title = Str(value, "title") ?? Str(value, "name") ?? id,
                Subtitle = Str(value, "subtitle"),
                Year = Str(value, "year"),
                Overview = Str(value, "overview") ?? Str(value, "description"),
                Poster = Uri.TryCreate(Str(value, "poster"), UriKind.Absolute, out var poster) ? poster : null,
                Backdrop = Uri.TryCreate(Str(value, "backdrop"), UriKind.Absolute, out var backdrop) ? backdrop : null,
                Rating = Num(value, "rating"),
                DurationMinutes = Int(value, "duration"),
                SeriesId = Str(value, "seriesId"),
                Season = Int(value, "season"),
                Episode = Int(value, "episode"),
                Extra = ToJson(Prop(value, "extra")) as JObject
            };

            foreach (var season in Items(Prop(value, "seasons")))
            {
                if (season.IsNumber())
                {
                    item.Seasons.Add(new LibrarySeason { Number = (int)season.AsNumber() });

                    continue;
                }

                if (!season.IsObject())
                {
                    continue;
                }

                item.Seasons.Add(new LibrarySeason
                {
                    Number = Int(season, "number") ?? item.Seasons.Count + 1,
                    Name = Str(season, "name"),
                    EpisodeCount = Int(season, "episodeCount") ?? Int(season, "episodes") ?? 0
                });
            }

            return item;
        }

        private static bool TryTime(JsValue value, out DateTimeOffset result)
        {
            result = default;

            if (value.IsNumber())
            {
                var number = value.AsNumber();

                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    return false;
                }

                result = number > 100000000000d ? DateTimeOffset.FromUnixTimeMilliseconds((long)number) : DateTimeOffset.FromUnixTimeSeconds((long)number);

                return true;
            }

            if (value.IsDate())
            {
                result = DateTimeOffset.FromUnixTimeMilliseconds((long)value.AsDate().ToDateTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);

                return true;
            }

            if (!value.IsString())
            {
                return false;
            }

            var text = value.AsString();

            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result) || XmltvParser.TryParseTime(text, out result);
        }

        private static IEnumerable<JsValue> Items(JsValue value)
        {
            if (value == null || value.IsUndefined() || value.IsNull())
            {
                yield break;
            }

            if (value.IsArray())
            {
                var array = value.AsArray();

                var length = (uint)array.Get("length").AsNumber();

                for (var i = 0u; i < length; i++)
                {
                    yield return array.Get(i.ToString(CultureInfo.InvariantCulture));
                }

                yield break;
            }

            if (value.IsObject())
            {
                var obj = value.AsObject();

                var lengthValue = obj.Get("length");

                if (lengthValue.IsNumber())
                {
                    var length = (uint)lengthValue.AsNumber();

                    for (var i = 0u; i < length; i++)
                    {
                        yield return obj.Get(i.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
        }

        private static JsValue Prop(JsValue value, string key)
        {
            if (value == null || !value.IsObject())
            {
                return JsValue.Undefined;
            }

            return value.AsObject().Get(key);
        }

        private static string Str(JsValue value, string key)
        {
            var prop = Prop(value, key);

            if (prop.IsUndefined() || prop.IsNull())
            {
                return null;
            }

            if (prop.IsString())
            {
                var text = prop.AsString();

                return string.IsNullOrWhiteSpace(text) ? null : text;
            }

            if (prop.IsNumber())
            {
                var number = prop.AsNumber();

                return Math.Abs(number % 1) < double.Epsilon ? ((long)number).ToString(CultureInfo.InvariantCulture) : number.ToString(CultureInfo.InvariantCulture);
            }

            return prop.ToString();
        }

        private static double? Num(JsValue value, string key)
        {
            var prop = Prop(value, key);

            if (prop.IsNumber())
            {
                var number = prop.AsNumber();

                return double.IsNaN(number) ? (double?)null : number;
            }

            if (prop.IsString() && double.TryParse(prop.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static int? Int(JsValue value, string key)
        {
            var number = Num(value, key);

            return number.HasValue ? (int?)(int)number.Value : null;
        }
    }

    public class ScriptException : Exception
    {
        public ScriptException(string message) : base(message)
        {
        }

        public ScriptException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
