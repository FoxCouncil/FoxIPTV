// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback.Hls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;

    public sealed class WebVttCue
    {
        public double Start { get; set; }

        public double End { get; set; }

        public string Text { get; set; }

        public override string ToString() => $"{Start:0.000}-{End:0.000} {Text}";
    }

    public static class WebVtt
    {
        public const double MpegTsHz = 90000;

        private static readonly Regex Tag = new Regex("<[^>]*>", RegexOptions.Compiled);

        public static List<WebVttCue> Parse(string text)
        {
            if (text == null)
            {
                return null;
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            if (!lines[0].TrimStart('﻿').StartsWith("WEBVTT", StringComparison.Ordinal))
            {
                return null;
            }

            var local = 0.0;
            var mpegts = 0.0;
            var i = 1;

            for (; i < lines.Length && lines[i].Trim().Length > 0 && !lines[i].Contains("-->"); i++)
            {
                if (lines[i].StartsWith("X-TIMESTAMP-MAP=", StringComparison.OrdinalIgnoreCase))
                {
                    ReadMap(lines[i].Substring("X-TIMESTAMP-MAP=".Length), ref local, ref mpegts);
                }
            }

            var cues = new List<WebVttCue>();

            while (i < lines.Length)
            {
                if (lines[i].Trim().Length == 0)
                {
                    i++;

                    continue;
                }

                var first = i;

                while (i < lines.Length && lines[i].Trim().Length > 0)
                {
                    i++;
                }

                var timing = Enumerable.Range(first, Math.Min(2, i - first)).FirstOrDefault(x => lines[x].Contains("-->"), -1);

                if (timing < 0 || !TryTiming(lines[timing], out var from, out var to) || to <= from)
                {
                    continue;
                }

                var body = string.Join("\n", lines.Skip(timing + 1).Take(i - timing - 1).Select(Clean).Where(x => x.Length > 0));

                if (body.Length == 0)
                {
                    continue;
                }

                cues.Add(new WebVttCue { Start = from - local + mpegts, End = to - local + mpegts, Text = body });
            }

            return cues;
        }

        private static bool TryTime(string text, out double seconds)
        {
            seconds = 0;

            var parts = text.Trim().Replace(',', '.').Split(':');

            if (parts.Length < 2 || parts.Length > 3)
            {
                return false;
            }

            var hours = 0L;

            if (parts.Length == 3 && !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours))
            {
                return false;
            }

            if (!long.TryParse(parts[parts.Length - 2], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || !double.TryParse(parts[parts.Length - 1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rest))
            {
                return false;
            }

            seconds = hours * 3600 + minutes * 60 + rest;

            return true;
        }

        private static void ReadMap(string value, ref double local, ref double mpegts)
        {
            foreach (var pair in value.Split(','))
            {
                var colon = pair.IndexOf(':');

                if (colon < 0)
                {
                    continue;
                }

                var name = pair.Substring(0, colon).Trim();
                var setting = pair.Substring(colon + 1).Trim();

                if (string.Equals(name, "LOCAL", StringComparison.OrdinalIgnoreCase) && TryTime(setting, out var seconds))
                {
                    local = seconds;
                }
                else if (string.Equals(name, "MPEGTS", StringComparison.OrdinalIgnoreCase) && long.TryParse(setting, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
                {
                    mpegts = ticks / MpegTsHz;
                }
            }
        }

        private static bool TryTiming(string line, out double from, out double to)
        {
            to = 0;

            var arrow = line.IndexOf("-->", StringComparison.Ordinal);
            var end = line.Substring(arrow + 3).Trim().Split(' ', '\t')[0];

            return TryTime(line.Substring(0, arrow), out from) & TryTime(end, out to);
        }

        private static string Clean(string line)
        {
            return WebUtility.HtmlDecode(Tag.Replace(line, string.Empty)).Replace(' ', ' ').Replace("‎", string.Empty).Replace("‏", string.Empty).Trim();
        }
    }
}
