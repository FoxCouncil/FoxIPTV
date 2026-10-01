// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Classes;
    using Newtonsoft.Json.Linq;

    public static class ProviderParts
    {
        public static readonly string[] Regions = { "us", "ca", "gb", "au", "nz", "de", "fr", "es", "it", "at", "ch", "dk", "no", "se", "in", "kr", "mx", "br", "ar", "cl", "all" };

        private static readonly Regex UnwantedName = new Regex(@"newsmax|america[’']?s voice|^the first( tv)?$|daily wire|salem news|\bfox\b|\boan\b|one america news|\bntd\b|\bepoch (tv|times)\b|lindell|frank speech|war room|right side broadcasting|\brsbn\b|\bblaze ?tv\b|\bthe blaze\b|\bblaze live\b|loomer|lionel nation|\bgb news\b|\bcbn news\b|merit street|merit tv|sky news australia|sky news (now )?\(au\)|\btalk ?tv\b|turning point|prageru|breitbart|daily caller|\bmr\.? ?beast\b|\bbeast games\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CountryPrefix = new Regex(@"^[A-Z]{2}: ", RegexOptions.Compiled);

        private const int GuidePagesAtOnce = 12;

        public static bool IsUnwanted(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && UnwantedName.IsMatch(CountryPrefix.Replace(name.Trim(), string.Empty));
        }

        public static string ShippedList(string name)
        {
            using (var stream = typeof(ProviderParts).Assembly.GetManifestResourceStream("FoxIPTV.Lists." + name))
            using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new StreamReader(unzipped, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public static Task<string> CachedList(string address, double hours)
        {
            return Web.GetStringCached(address, "list-" + address.ToMD5(), hours);
        }

        public static async Task<List<Programme>> XmltvGuide(string address, double hours, IProgress<int> progress)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return new List<Programme>();
            }

            return XmltvParser.Parse(await CachedList(address, hours).ConfigureAwait(false), progress);
        }

        public static async Task<string[]> GuidePages(IReadOnlyList<string> addresses, string cachePrefix, double hours, IDictionary<string, string> headers, IProgress<int> progress, string title)
        {
            var pages = new string[addresses.Count];
            var done = 0;

            using (var gate = new SemaphoreSlim(GuidePagesAtOnce))
            {
                await Task.WhenAll(addresses.Select(async (address, index) =>
                {
                    await gate.WaitAsync().ConfigureAwait(false);

                    try
                    {
                        pages[index] = await Web.GetStringCached(address, cachePrefix + address.ToMD5(), hours, headers).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        TvCore.LogDebug($"[{title}] Guide page failed {address}: {ex.Message}");
                    }
                    finally
                    {
                        gate.Release();

                        var count = Interlocked.Increment(ref done);

                        if (count % 25 == 0)
                        {
                            progress?.Report(count * 100 / addresses.Count);
                        }
                    }
                })).ConfigureAwait(false);
            }

            return pages;
        }

        public static ProviderField RegionField(string defaultRegion = "us")
        {
            return ProviderField.Choice("Region", Regions, defaultRegion);
        }

        public static string Setting(JObject data, string key, string fallback)
        {
            var value = data?[key]?.ToString();

            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        public static string Region(JObject data, IReadOnlyList<string> offered, string title)
        {
            var region = Setting(data, "Region", "us").ToLowerInvariant();

            if (offered != null && !offered.Contains(region))
            {
                TvCore.LogInfo($"[{title}] Region '{region}' not offered by this source, using '{offered[offered.Count - 1]}'");

                region = offered[offered.Count - 1];
            }

            return region;
        }

        public static List<Channel> Numbered(IEnumerable<Channel> channels)
        {
            var used = new HashSet<uint>();
            var next = 1u;
            var numbered = new List<Channel>();

            foreach (var channel in channels)
            {
                if (channel.Index == 0 || used.Contains(channel.Index))
                {
                    while (used.Contains(next))
                    {
                        next++;
                    }

                    channel.Index = next;
                }

                used.Add(channel.Index);
                numbered.Add(channel);
            }

            return numbered.OrderBy(x => x.Index).ToList();
        }

        public static List<Channel> FromPlaylist(M3UPlaylist playlist)
        {
            var channels = new List<Channel>();

            foreach (var entry in playlist.Entries)
            {
                if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var stream))
                {
                    continue;
                }

                channels.Add(new Channel
                {
                    Index = entry.Index,
                    Id = entry.Id ?? string.Empty,
                    Name = entry.Name ?? stream.Host,
                    Group = string.IsNullOrWhiteSpace(entry.Group) ? "Uncategorized" : entry.Group,
                    Logo = Uri.TryCreate(entry.Logo, UriKind.Absolute, out var logo) ? logo : null,
                    Stream = stream
                });
            }

            return Numbered(channels);
        }

        public static Programme Programme(string channel, DateTimeOffset start, DateTimeOffset stop, string title, string description)
        {
            return new Programme
            {
                Channel = channel ?? string.Empty,
                Start = start,
                Stop = stop,
                Title = title ?? string.Empty,
                Description = description ?? string.Empty
            };
        }
    }
}
