// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using Classes;
    using Newtonsoft.Json.Linq;

    public static class ProviderParts
    {
        public static readonly string[] Regions = { "us", "ca", "gb", "au", "nz", "de", "fr", "es", "it", "at", "ch", "dk", "no", "se", "in", "kr", "mx", "br", "ar", "cl", "all" };

        public static string ShippedList(string name)
        {
            using (var stream = typeof(ProviderParts).Assembly.GetManifestResourceStream("FoxIPTV.Lists." + name))
            using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new StreamReader(unzipped, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public static ProviderField RegionField()
        {
            return ProviderField.Choice("Region", Regions, "us");
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
                Description = description ?? string.Empty,
                BlockLength = (int)Math.Floor((stop - start).TotalMinutes / 10d)
            };
        }
    }
}
