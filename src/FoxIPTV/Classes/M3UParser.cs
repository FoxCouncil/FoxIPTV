// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    /// <summary>A single entry of an M3U playlist</summary>
    public class M3UEntry
    {
        /// <summary>The channel number, from tvg-chno or the position in the file</summary>
        public uint Index { get; set; }

        /// <summary>The guide identifier, from tvg-id</summary>
        public string Id { get; set; }

        /// <summary>The display name, after the comma on the EXTINF line</summary>
        public string Name { get; set; }

        /// <summary>The group, from group-title or EXTGRP</summary>
        public string Group { get; set; }

        /// <summary>The logo URL, from tvg-logo</summary>
        public string Logo { get; set; }

        /// <summary>The stream URL</summary>
        public string Url { get; set; }

        /// <summary>Every key="value" attribute on the EXTINF line</summary>
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every EXTVLCOPT option, e.g. http-user-agent or http-referrer</summary>
        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A parsed M3U playlist</summary>
    public class M3UPlaylist
    {
        /// <summary>The guide URLs declared in the header via url-tvg or x-tvg-url</summary>
        public List<string> GuideUrls { get; set; } = new List<string>();

        /// <summary>The entries, in file order</summary>
        public List<M3UEntry> Entries { get; set; } = new List<M3UEntry>();
    }

    /// <summary>A tolerant parser for extended M3U playlists as used by IPTV</summary>
    public static class M3UParser
    {
        /// <summary>Matches key="value" pairs on the EXTINF and EXTM3U lines</summary>
        private static readonly Regex AttributeRegex = new Regex("([A-Za-z0-9_\\-]+)=\"([^\"]*)\"", RegexOptions.Compiled);

        /// <summary>Parse playlist text</summary>
        /// <param name="text">The full playlist contents</param>
        /// <returns>A parsed playlist, never null</returns>
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

                    if (line.StartsWith("#EXTVLCOPT:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pending != null)
                        {
                            var option = line.Substring(11);
                            var eq = option.IndexOf('=');

                            if (eq > 0)
                            {
                                pending.Options[option.Substring(0, eq).Trim()] = option.Substring(eq + 1).Trim();
                            }
                        }

                        continue;
                    }

                    if (line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Anything else is a URL for the pending entry
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

        /// <summary>Find the comma that separates the EXTINF attributes from the display name, ignoring commas inside quotes</summary>
        /// <param name="line">The EXTINF line</param>
        /// <returns>The index of the separating comma, or -1</returns>
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
