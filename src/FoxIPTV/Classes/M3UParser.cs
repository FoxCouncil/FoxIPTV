// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    public class M3UEntry
    {
        public uint Index { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public string Group { get; set; }

        public string Logo { get; set; }

        public string Url { get; set; }

        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public class M3UPlaylist
    {
        public List<string> GuideUrls { get; set; } = new List<string>();

        public List<M3UEntry> Entries { get; set; } = new List<M3UEntry>();
    }

    public static class M3UParser
    {
        private static readonly Regex AttributeRegex = new Regex("([A-Za-z0-9_\\-]+)=\"([^\"]*)\"", RegexOptions.Compiled);

        public static M3UPlaylist Parse(string text)
        {
            var playlist = new M3UPlaylist();

            if (string.IsNullOrWhiteSpace(text))
            {
                return playlist;
            }

            M3UEntry pending = null;

            var position = 0u;

            using (var reader = new StringReader(text))
            {
                string rawLine;

                while ((rawLine = reader.ReadLine()) != null)
                {
                    var line = rawLine.Trim();

                    if (line.Length == 0)
                    {
                        continue;
                    }

                    if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (Match match in AttributeRegex.Matches(line))
                        {
                            var key = match.Groups[1].Value;

                            if (key.Equals("url-tvg", StringComparison.OrdinalIgnoreCase) || key.Equals("x-tvg-url", StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var url in match.Groups[2].Value.Split(','))
                                {
                                    var trimmed = url.Trim();

                                    if (trimmed.Length > 0)
                                    {
                                        playlist.GuideUrls.Add(trimmed);
                                    }
                                }
                            }
                        }

                        continue;
                    }

                    if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
                    {
                        pending = new M3UEntry();

                        var commaIdx = FindNameComma(line);

                        var head = commaIdx >= 0 ? line.Substring(0, commaIdx) : line;

                        pending.Name = commaIdx >= 0 ? line.Substring(commaIdx + 1).Trim() : string.Empty;

                        foreach (Match match in AttributeRegex.Matches(head))
                        {
                            pending.Attributes[match.Groups[1].Value] = match.Groups[2].Value;
                        }

                        pending.Attributes.TryGetValue("tvg-id", out var id);
                        pending.Attributes.TryGetValue("tvg-logo", out var logo);
                        pending.Attributes.TryGetValue("group-title", out var group);
                        pending.Attributes.TryGetValue("tvg-chno", out var chno);

                        if (string.IsNullOrWhiteSpace(pending.Name) && pending.Attributes.TryGetValue("tvg-name", out var tvgName))
                        {
                            pending.Name = tvgName;
                        }

                        pending.Id = id ?? string.Empty;
                        pending.Logo = logo;
                        pending.Group = string.IsNullOrWhiteSpace(group) ? "Uncategorized" : group;
                        pending.Index = uint.TryParse(chno, out var parsedChno) && parsedChno > 0 ? parsedChno : 0;

                        continue;
                    }

                    if (line.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pending != null)
                        {
                            pending.Group = line.Substring(8).Trim();
                        }

                        continue;
                    }

                    if (line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (pending == null)
                    {
                        pending = new M3UEntry { Name = line, Group = "Uncategorized", Id = string.Empty };
                    }

                    pending.Url = line;

                    position++;

                    if (pending.Index == 0)
                    {
                        pending.Index = position;
                    }

                    playlist.Entries.Add(pending);

                    pending = null;
                }
            }

            return playlist;
        }

        private static int FindNameComma(string line)
        {
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
