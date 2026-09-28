// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

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

        private static readonly object Lock = new object();

        private static string _lastCreative;

        private static DateTime _breakStarted;

        private static bool _entryDiscontinuitySeen;

        private static double _breakSeconds;

        private static double _fetchedSeconds;

        private static bool _held;

        private static bool _breakNext;

        public static bool InAd { get; private set; }

        public static int AdNumber { get; private set; }

        public static double? SecondsLeft { get; private set; }

        public static void Reset()
        {
            lock (Lock)
            {
                Clear();
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
        }

        public static void ObserveSegment(string address, string title = null)
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

            lock (Lock)
            {
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

        private static string Short(string url)
        {
            return url.Length > 160 ? url.Substring(0, 160) : url;
        }
    }
}
