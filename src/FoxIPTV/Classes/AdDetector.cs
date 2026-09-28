// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using Playback.Hls;

    public static class AdDetector
    {
        private static readonly Regex AdPiece = new Regex(@"_ad(?:/|_bumper)|/creative/|Pluto_TV_OandO|plutotv_filler|dai\.google\.com|/v1/segment/|unified-ad-segment-cdn|[?&]media_type=A(?:&|$)|[?&]break_type=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Creative = new Regex(@"creative/([0-9a-f]{16,})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CueOut = new Regex(@"cue-out-(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PieceLength = new Regex(@"[?&]dur=(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SplitBeforeBreak = new Regex(@"/split_[^/]+/[^/]+_a\.[a-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SplitAfterBreak = new Regex(@"/split_[^/]+/[^/]+_b\.[a-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BreakBumper = new Regex(@"/modified_bumpers/", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ProgrammeTitle = new Regex(@"(?:^|[,;\s])pid=\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly TimeSpan LongestHeldBreak = TimeSpan.FromMinutes(15);

        private const double CueGraceSeconds = 2;

        private static readonly object Lock = new object();

        private static string _lastCreative;

        private static DateTime _breakStarted;

        private static bool _entryDiscontinuitySeen;

        private static double _breakSeconds;

        private static double _fetchedSeconds;

        private static bool _held;

        private static bool _breakNext;

        private static bool _cued;

        private static double _cueLength;

        private static double _cuePlayed;

        private static string _cueId;

        private static readonly HashSet<string> FinishedCueIds = new HashSet<string>(StringComparer.Ordinal);

        public static bool InAd { get; private set; }

        public static int AdNumber { get; private set; }

        public static double? SecondsLeft { get; private set; }

        public static void Reset()
        {
            lock (Lock)
            {
                Clear();

                FinishedCueIds.Clear();
            }
        }

        private static void Clear()
        {
            InAd = false;
            AdNumber = 0;
            SecondsLeft = null;
            _lastCreative = null;
            _entryDiscontinuitySeen = false;
            _breakSeconds = 0;
            _fetchedSeconds = 0;
            _held = false;
            _breakNext = false;
            _cued = false;
            _cueLength = 0;
            _cuePlayed = 0;
            _cueId = null;
        }

        public static void ObserveSegment(string address, string title = null, IReadOnlyList<string> marks = null, double duration = 0)
        {
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            var url = Uri.UnescapeDataString(address);
            var path = url.Split('?')[0];

            if (path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".webvtt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".key", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var isAd = AdPiece.IsMatch(url);
            var isProgramme = SplitAfterBreak.IsMatch(path) || title != null && ProgrammeTitle.IsMatch(title);

            ReadCues(marks, out var breakStarts, out var breakEnds, out var breakLength, out var breakElapsed, out var breakId);

            lock (Lock)
            {
                if (_cued && breakEnds)
                {
                    TvCore.LogInfo($"[Ads] Cue-in, break over after {(DateTime.UtcNow - _breakStarted).TotalSeconds:0}s: {Short(url)}");

                    Finish();
                }

                if (_cued && breakStarts && breakId != null && _cueId != null && breakId != _cueId && !FinishedCueIds.Contains(breakId))
                {
                    FinishedCueIds.Add(_cueId);

                    AdNumber++;
                    _cueId = breakId;
                    _cueLength = breakLength;
                    _cuePlayed = 0;

                    TvCore.LogInfo($"[Ads] Next ad in the break, #{AdNumber}{(breakLength > 0 ? $" of {breakLength:0}s" : string.Empty)}: {Short(url)}");
                }

                if (breakStarts && !_cued && (breakId == null || !FinishedCueIds.Contains(breakId)))
                {
                    Clear();

                    InAd = true;
                    AdNumber = 1;
                    _cued = true;
                    _cueId = breakId;
                    _breakStarted = DateTime.UtcNow;

                    TvCore.LogInfo($"[Ads] Cue-out, break{(breakLength > 0 ? $" of {breakLength:0}s" : string.Empty)} started: {Short(url)}");
                }

                if (_cued)
                {
                    if (breakLength > 0)
                    {
                        _cueLength = breakLength;
                    }

                    if (breakElapsed >= 0)
                    {
                        _cuePlayed = breakElapsed;
                    }

                    var overdue = _cueLength > 0 ? _cuePlayed >= _cueLength + CueGraceSeconds : DateTime.UtcNow - _breakStarted > LongestHeldBreak;

                    if (!overdue)
                    {
                        SecondsLeft = _cueLength > 0 ? Math.Max(0, _cueLength - _cuePlayed) : (double?)null;

                        _cuePlayed += duration;

                        return;
                    }

                    TvCore.LogInfo($"[Ads] Break ran its full {_cueLength:0}s with no cue-in: {Short(url)}");

                    Finish();
                }

                if (_held)
                {
                    if (!isProgramme && DateTime.UtcNow - _breakStarted < LongestHeldBreak)
                    {
                        return;
                    }

                    TvCore.LogInfo($"[Ads] Programme piece fetched, break over after {(DateTime.UtcNow - _breakStarted).TotalSeconds:0}s: {Short(url)}");

                    Clear();
                }

                if (_breakNext || BreakBumper.IsMatch(path))
                {
                    var atBumper = !_breakNext;

                    Clear();

                    InAd = true;
                    AdNumber = 1;
                    _held = true;
                    _breakStarted = DateTime.UtcNow;

                    TvCore.LogInfo($"[Ads] Break started {(atBumper ? "at a bumper" : "after a split programme piece")}: {Short(url)}");

                    return;
                }

                if (!isAd)
                {
                    if (InAd)
                    {
                        TvCore.LogInfo($"[Ads] Programme piece fetched, break over after {(DateTime.UtcNow - _breakStarted).TotalSeconds:0}s and {AdNumber} ad(s)");

                        Clear();
                    }

                    _breakNext = SplitBeforeBreak.IsMatch(path);

                    return;
                }

                var match = Creative.Match(url);
                var creative = match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;

                if (!InAd)
                {
                    Clear();

                    InAd = true;
                    AdNumber = 1;
                    _breakStarted = DateTime.UtcNow;
                    _lastCreative = creative;

                    TvCore.LogInfo($"[Ads] Ad piece fetched, break started: {Short(url)}");
                }
                else if (creative != null && creative != _lastCreative)
                {
                    AdNumber++;
                    _lastCreative = creative;

                    TvCore.LogInfo($"[Ads] Next ad in the break, #{AdNumber}: {Short(url)}");
                }

                var cueOut = CueOut.Match(url);
                var length = PieceLength.Match(url);

                if (cueOut.Success)
                {
                    _breakSeconds = double.Parse(cueOut.Groups[1].Value, CultureInfo.InvariantCulture);
                }

                if (_breakSeconds > 0)
                {
                    SecondsLeft = Math.Max(0, _breakSeconds - _fetchedSeconds);

                    if (length.Success)
                    {
                        _fetchedSeconds += double.Parse(length.Groups[1].Value, CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        public static void ObserveDiscontinuity()
        {
            lock (Lock)
            {
                if (!InAd || _held)
                {
                    return;
                }

                if (!_entryDiscontinuitySeen)
                {
                    _entryDiscontinuitySeen = true;

                    return;
                }

                if (_lastCreative != null)
                {
                    return;
                }

                AdNumber++;

                TvCore.LogInfo($"[Ads] Next ad in the break, #{AdNumber} (discontinuity)");
            }
        }

        private static void Finish()
        {
            if (_cueId != null)
            {
                FinishedCueIds.Add(_cueId);
            }

            Clear();
        }

        private static void ReadCues(IReadOnlyList<string> marks, out bool cueOut, out bool cueIn, out double length, out double elapsed, out string id)
        {
            cueOut = false;
            cueIn = false;
            length = 0;
            elapsed = -1;
            id = null;

            if (marks == null)
            {
                return;
            }

            foreach (var mark in marks)
            {
                if (string.IsNullOrEmpty(mark))
                {
                    continue;
                }

                var colon = mark.IndexOf(':');
                var tag = colon < 0 ? mark : mark.Substring(0, colon);
                var value = colon < 0 ? string.Empty : mark.Substring(colon + 1);

                switch (tag)
                {
                    case "#EXT-X-CUE-OUT":
                    {
                        cueOut = true;
                        length = value.Contains('=') ? Number(HlsPlaylist.Attributes(value), "DURATION") : Number(value);
                    }
                    break;

                    case "#EXT-X-CUE-OUT-CONT":
                    {
                        cueOut = true;

                        var slash = value.IndexOf('/');

                        if (slash > 0 && !value.Contains('='))
                        {
                            elapsed = Number(value.Substring(0, slash));
                            length = Number(value.Substring(slash + 1));
                        }
                        else
                        {
                            var attributes = HlsPlaylist.Attributes(value);

                            elapsed = attributes.ContainsKey("ElapsedTime") ? Number(attributes, "ElapsedTime") : -1;
                            length = Number(attributes, "Duration");
                        }
                    }
                    break;

                    case "#EXT-X-CUE-IN":
                    {
                        cueIn = true;
                    }
                    break;

                    case "#EXT-X-DATERANGE":
                    {
                        var attributes = HlsPlaylist.Attributes(value);

                        if (attributes.ContainsKey("SCTE35-OUT"))
                        {
                            cueOut = true;
                            length = Number(attributes, attributes.ContainsKey("DURATION") ? "DURATION" : "PLANNED-DURATION");
                            id = attributes.TryGetValue("ID", out var rangeId) ? rangeId : null;
                        }

                        if (attributes.ContainsKey("SCTE35-IN"))
                        {
                            cueIn = true;
                        }
                    }
                    break;

                    case "#EXT-X-AD-START":
                    {
                        var attributes = HlsPlaylist.Attributes(value);

                        if (attributes.TryGetValue("URI", out var uri) && Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
                        {
                            var query = System.Web.HttpUtility.ParseQueryString(parsed.Query);

                            cueOut = true;
                            length = Number(query["dur"]);
                            id = query["id"];
                        }
                    }
                    break;
                }
            }
        }

        private static double Number(IDictionary<string, string> attributes, string key)
        {
            return attributes.TryGetValue(key, out var value) ? Number(value) : 0;
        }

        private static double Number(string value)
        {
            return double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0 ? number : 0;
        }

        private static string Short(string url)
        {
            return url.Length > 160 ? url.Substring(0, 160) : url;
        }
    }
}
