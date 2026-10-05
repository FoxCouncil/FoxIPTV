// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback.Hls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    public sealed class HlsVariant
    {
        public Uri Uri { get; set; }

        public long Bandwidth { get; set; }

        public long AverageBandwidth { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public double FrameRate { get; set; }

        public string Codecs { get; set; }

        public string AudioGroup { get; set; }

        public string SubtitleGroup { get; set; }

        public string ClosedCaptions { get; set; }

        public bool HasVideo => Width > 0 || Height > 0 || HasVideoCodec;

        private bool HasVideoCodec => !string.IsNullOrEmpty(Codecs) && Codecs.Split(',').Any(x => x.Trim().StartsWith("avc", StringComparison.OrdinalIgnoreCase) || x.Trim().StartsWith("hvc", StringComparison.OrdinalIgnoreCase) || x.Trim().StartsWith("hev", StringComparison.OrdinalIgnoreCase) || x.Trim().StartsWith("mp4v", StringComparison.OrdinalIgnoreCase) || x.Trim().StartsWith("av01", StringComparison.OrdinalIgnoreCase) || x.Trim().StartsWith("vp09", StringComparison.OrdinalIgnoreCase));

        public override string ToString() => $"{Bandwidth / 1000}kbps {(Height > 0 ? $"{Width}x{Height}" : "no size")} {Codecs}";
    }

    public sealed class HlsRendition
    {
        public string Type { get; set; }

        public string GroupId { get; set; }

        public string Name { get; set; }

        public string Language { get; set; }

        public bool IsDefault { get; set; }

        public bool AutoSelect { get; set; }

        public bool IsForced { get; set; }

        public string InstreamId { get; set; }

        public Uri Uri { get; set; }

        public override string ToString() => $"{Type} {GroupId} {Name} {Language}{(IsDefault ? " default" : string.Empty)}{(IsForced ? " forced" : string.Empty)}";
    }

    public sealed class HlsKey
    {
        public string Method { get; set; } = "NONE";

        public Uri Uri { get; set; }

        public byte[] Iv { get; set; }

        public string KeyFormat { get; set; } = "identity";

        public bool IsNone => string.Equals(Method, "NONE", StringComparison.OrdinalIgnoreCase);

        public bool IsAes128 => string.Equals(Method, "AES-128", StringComparison.OrdinalIgnoreCase) && string.Equals(KeyFormat, "identity", StringComparison.OrdinalIgnoreCase);

        public bool IsCopyProtection => !IsNone && !IsAes128;
    }

    public sealed class HlsMap
    {
        public Uri Uri { get; set; }

        public long? Offset { get; set; }

        public long? Length { get; set; }

        public HlsKey Key { get; set; }

        public string Id => $"{Uri}@{Offset}+{Length}";
    }

    public sealed class HlsSegment
    {
        public Uri Uri { get; set; }

        public double Duration { get; set; }

        public string Title { get; set; }

        public long Sequence { get; set; }

        public int DiscontinuitySequence { get; set; }

        public bool Discontinuity { get; set; }

        public HlsKey Key { get; set; }

        public HlsMap Map { get; set; }

        public long? Offset { get; set; }

        public long? Length { get; set; }

        public DateTimeOffset? ProgramDateTime { get; set; }

        public bool Gap { get; set; }

        public List<string> Marks { get; } = new List<string>();

        public override string ToString() => $"#{Sequence} d{DiscontinuitySequence} {Duration:0.000}s {Uri}";
    }

    public sealed class HlsPlaylist
    {
        public Uri Uri { get; set; }

        public bool IsMaster => Variants.Count > 0;

        public List<HlsVariant> Variants { get; } = new List<HlsVariant>();

        public List<HlsRendition> Renditions { get; } = new List<HlsRendition>();

        public List<HlsKey> SessionKeys { get; } = new List<HlsKey>();

        public double TargetDuration { get; set; }

        public long MediaSequence { get; set; }

        public int DiscontinuitySequence { get; set; }

        public bool EndList { get; set; }

        public string PlaylistType { get; set; }

        public List<HlsSegment> Segments { get; } = new List<HlsSegment>();

        public Dictionary<string, string> UnreadTags { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool IsLive => !EndList && !string.Equals(PlaylistType, "VOD", StringComparison.OrdinalIgnoreCase);

        public double TotalDuration => Segments.Sum(x => x.Duration);

        public static bool LooksLikePlaylist(string text)
        {
            return text != null && text.TrimStart('﻿', ' ', '\t', '\r', '\n').StartsWith("#EXT", StringComparison.Ordinal);
        }

        public static HlsPlaylist Parse(string text, Uri uri)
        {
            var playlist = new HlsPlaylist { Uri = uri };

            HlsVariant pendingVariant = null;
            HlsKey key = new HlsKey();
            HlsMap map = null;

            var sequence = -1L;
            var discontinuitySequence = 0;
            var discontinuity = false;
            var duration = 0.0;
            string title = null;
            var hasDuration = false;
            long? offset = null;
            long? length = null;
            long nextOffset = 0;
            Uri lastRangeUri = null;
            DateTimeOffset? programDateTime = null;
            var gap = false;
            var marks = new List<string>();

            using (var reader = new StringReader(text ?? string.Empty))
            {
                string raw;

                while ((raw = reader.ReadLine()) != null)
                {
                    var line = raw.Trim().TrimStart('﻿');

                    if (line.Length == 0)
                    {
                        continue;
                    }

                    if (!line.StartsWith("#", StringComparison.Ordinal))
                    {
                        var target = Resolve(uri, line);

                        if (pendingVariant != null)
                        {
                            pendingVariant.Uri = target;
                            playlist.Variants.Add(pendingVariant);
                            pendingVariant = null;

                            continue;
                        }

                        if (!hasDuration)
                        {
                            continue;
                        }

                        if (sequence < 0)
                        {
                            sequence = playlist.MediaSequence;
                        }

                        var segment = new HlsSegment
                        {
                            Uri = target,
                            Duration = duration,
                            Title = title,
                            Sequence = sequence,
                            DiscontinuitySequence = discontinuitySequence,
                            Discontinuity = discontinuity,
                            Key = key,
                            Map = map,
                            ProgramDateTime = programDateTime,
                            Gap = gap
                        };

                        if (length.HasValue)
                        {
                            var start = offset ?? (lastRangeUri == target ? nextOffset : 0);

                            segment.Offset = start;
                            segment.Length = length;

                            nextOffset = start + length.Value;
                            lastRangeUri = target;
                        }

                        segment.Marks.AddRange(marks);

                        playlist.Segments.Add(segment);

                        if (programDateTime.HasValue)
                        {
                            programDateTime = programDateTime.Value.AddSeconds(duration);
                        }

                        sequence++;
                        discontinuity = false;
                        hasDuration = false;
                        duration = 0;
                        title = null;
                        offset = null;
                        length = null;
                        gap = false;
                        marks.Clear();

                        continue;
                    }

                    var colon = line.IndexOf(':');
                    var tag = colon < 0 ? line : line.Substring(0, colon);
                    var value = colon < 0 ? string.Empty : line.Substring(colon + 1);

                    switch (tag)
                    {
                        case "#EXT-X-STREAM-INF":
                        {
                            var attributes = Attributes(value);

                            pendingVariant = new HlsVariant
                            {
                                Bandwidth = Long(attributes, "BANDWIDTH"),
                                AverageBandwidth = Long(attributes, "AVERAGE-BANDWIDTH"),
                                Codecs = Text(attributes, "CODECS"),
                                AudioGroup = Text(attributes, "AUDIO"),
                                SubtitleGroup = Text(attributes, "SUBTITLES"),
                                ClosedCaptions = Text(attributes, "CLOSED-CAPTIONS"),
                                FrameRate = Double(attributes, "FRAME-RATE")
                            };

                            var resolution = Text(attributes, "RESOLUTION");

                            if (!string.IsNullOrEmpty(resolution))
                            {
                                var parts = resolution.Split('x', 'X');

                                if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
                                {
                                    pendingVariant.Width = width;
                                    pendingVariant.Height = height;
                                }
                            }
                        }
                        break;

                        case "#EXT-X-MEDIA":
                        {
                            var attributes = Attributes(value);
                            var mediaUri = Text(attributes, "URI");

                            playlist.Renditions.Add(new HlsRendition
                            {
                                Type = Text(attributes, "TYPE"),
                                GroupId = Text(attributes, "GROUP-ID"),
                                Name = Text(attributes, "NAME"),
                                Language = Text(attributes, "LANGUAGE"),
                                IsDefault = string.Equals(Text(attributes, "DEFAULT"), "YES", StringComparison.OrdinalIgnoreCase),
                                AutoSelect = string.Equals(Text(attributes, "AUTOSELECT"), "YES", StringComparison.OrdinalIgnoreCase),
                                IsForced = string.Equals(Text(attributes, "FORCED"), "YES", StringComparison.OrdinalIgnoreCase),
                                InstreamId = Text(attributes, "INSTREAM-ID"),
                                Uri = string.IsNullOrEmpty(mediaUri) ? null : Resolve(uri, mediaUri)
                            });
                        }
                        break;

                        case "#EXT-X-SESSION-KEY":
                        {
                            playlist.SessionKeys.Add(ParseKey(value, uri));
                        }
                        break;

                        case "#EXT-X-TARGETDURATION":
                        {
                            playlist.TargetDuration = ParseDouble(value);
                        }
                        break;

                        case "#EXT-X-MEDIA-SEQUENCE":
                        {
                            playlist.MediaSequence = long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mediaSequence) ? mediaSequence : 0;
                        }
                        break;

                        case "#EXT-X-DISCONTINUITY-SEQUENCE":
                        {
                            playlist.DiscontinuitySequence = int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ? start : 0;
                            discontinuitySequence = playlist.DiscontinuitySequence;
                        }
                        break;

                        case "#EXT-X-ENDLIST":
                        {
                            playlist.EndList = true;
                        }
                        break;

                        case "#EXT-X-PLAYLIST-TYPE":
                        {
                            playlist.PlaylistType = value.Trim();
                        }
                        break;

                        case "#EXT-X-KEY":
                        {
                            key = ParseKey(value, uri);
                        }
                        break;

                        case "#EXT-X-MAP":
                        {
                            var attributes = Attributes(value);

                            map = new HlsMap { Uri = Resolve(uri, Text(attributes, "URI")), Key = key };

                            var range = Text(attributes, "BYTERANGE");

                            if (!string.IsNullOrEmpty(range))
                            {
                                ParseRange(range, out var mapLength, out var mapOffset);

                                map.Length = mapLength;
                                map.Offset = mapOffset ?? 0;
                            }
                        }
                        break;

                        case "#EXTINF":
                        {
                            var comma = value.IndexOf(',');

                            duration = ParseDouble(comma < 0 ? value : value.Substring(0, comma));
                            title = comma < 0 || string.IsNullOrWhiteSpace(value.Substring(comma + 1)) ? null : value.Substring(comma + 1).Trim();
                            hasDuration = true;
                        }
                        break;

                        case "#EXT-X-BYTERANGE":
                        {
                            ParseRange(value, out var rangeLength, out var rangeOffset);

                            length = rangeLength;
                            offset = rangeOffset;
                        }
                        break;

                        case "#EXT-X-DISCONTINUITY":
                        {
                            discontinuity = true;
                            discontinuitySequence++;
                        }
                        break;

                        case "#EXT-X-PROGRAM-DATE-TIME":
                        {
                            programDateTime = DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when) ? when : (DateTimeOffset?)null;
                        }
                        break;

                        case "#EXT-X-GAP":
                        {
                            gap = true;
                        }
                        break;

                        case "#EXT-X-CUE-OUT":
                        case "#EXT-X-CUE-OUT-CONT":
                        case "#EXT-X-CUE-IN":
                        case "#EXT-X-CUE":
                        case "#EXT-X-DATERANGE":
                        case "#EXT-X-SCTE35":
                        case "#EXT-OATCLS-SCTE35":
                        case "#EXT-X-ASSET":
                        case "#EXT-X-AD-START":
                        {
                            marks.Add(line);
                        }
                        break;

                        default:
                        {
                            playlist.UnreadTags.TryAdd(tag, line);
                        }
                        break;
                    }
                }
            }

            return playlist;
        }

        public static Dictionary<string, string> Attributes(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var i = 0;

            while (i < text.Length)
            {
                while (i < text.Length && (text[i] == ',' || text[i] == ' '))
                {
                    i++;
                }

                var equals = text.IndexOf('=', i);

                if (equals < 0)
                {
                    break;
                }

                var name = text.Substring(i, equals - i).Trim();

                i = equals + 1;

                string value;

                if (i < text.Length && text[i] == '"')
                {
                    var close = text.IndexOf('"', i + 1);

                    if (close < 0)
                    {
                        close = text.Length;
                    }

                    value = text.Substring(i + 1, close - i - 1);
                    i = close + 1;
                }
                else
                {
                    var comma = text.IndexOf(',', i);

                    if (comma < 0)
                    {
                        comma = text.Length;
                    }

                    value = text.Substring(i, comma - i).Trim();
                    i = comma;
                }

                result[name] = value;
            }

            return result;
        }

        private static HlsKey ParseKey(string value, Uri baseUri)
        {
            var attributes = Attributes(value);
            var keyUri = Text(attributes, "URI");
            var iv = Text(attributes, "IV");

            return new HlsKey
            {
                Method = Text(attributes, "METHOD") ?? "NONE",
                Uri = string.IsNullOrEmpty(keyUri) || keyUri.StartsWith("skd:", StringComparison.OrdinalIgnoreCase) || keyUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? null : Resolve(baseUri, keyUri),
                Iv = string.IsNullOrEmpty(iv) ? null : Hex(iv),
                KeyFormat = Text(attributes, "KEYFORMAT") ?? "identity"
            };
        }

        private static void ParseRange(string value, out long length, out long? offset)
        {
            var parts = value.Trim().Split('@');

            length = long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLength) ? parsedLength : 0;
            offset = parts.Length > 1 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOffset) ? parsedOffset : (long?)null;
        }

        private static byte[] Hex(string text)
        {
            var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.Substring(2) : text;

            if (hex.Length % 2 == 1)
            {
                hex = "0" + hex;
            }

            var bytes = new byte[16];
            var data = Convert.FromHexString(hex);

            Array.Copy(data, Math.Max(0, data.Length - 16), bytes, Math.Max(0, 16 - data.Length), Math.Min(16, data.Length));

            return bytes;
        }

        private static Uri Resolve(Uri baseUri, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return baseUri == null ? new Uri(value, UriKind.RelativeOrAbsolute) : new Uri(baseUri, value);
        }

        private static string Text(Dictionary<string, string> attributes, string name)
        {
            return attributes.TryGetValue(name, out var value) ? value : null;
        }

        private static long Long(Dictionary<string, string> attributes, string name)
        {
            return attributes.TryGetValue(name, out var value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        private static double Double(Dictionary<string, string> attributes, string name)
        {
            return attributes.TryGetValue(name, out var value) ? ParseDouble(value) : 0;
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        public string Describe()
        {
            var text = new StringBuilder();

            if (IsMaster)
            {
                text.Append($"master, {Variants.Count} variant(s): {string.Join("; ", Variants)}");

                if (Renditions.Count > 0)
                {
                    text.Append($"; renditions: {string.Join("; ", Renditions)}");
                }
            }
            else
            {
                text.Append($"media, {Segments.Count} segment(s) from #{MediaSequence}, target {TargetDuration:0.#}s, {TotalDuration:0.#}s total, {(IsLive ? "live" : "on demand")}");
            }

            return text.ToString();
        }
    }
}
