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

        private const string VideoDiscontinuity = "Restarting demuxer 0 1";

        private static readonly object Lock = new object();

        private static string _lastCreative;

        private static DateTime _breakStarted;

        private static bool _entryDiscontinuitySeen;

        private static double _breakSeconds;

        private static double _fetchedSeconds;

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
        }

        public static void Observe(string message)
        {
            if (message == null)
            {
                return;
            }

            if (message.StartsWith(VideoDiscontinuity, StringComparison.Ordinal))
            {
                ObserveDiscontinuity();

                return;
            }

            if (!message.StartsWith("Retrieving http", StringComparison.Ordinal))
            {
                return;
            }

            ObserveSegment(message.Substring("Retrieving ".Length).Trim());
        }

        /// <summary>Called as each video piece starts playing</summary>
        public static void ObserveSegment(string address)
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

            lock (Lock)
            {
                if (!isAd)
                {
                    if (InAd)
                    {
                        TvCore.LogInfo($"[Ads] Programme piece fetched, break over after {(DateTime.UtcNow - _breakStarted).TotalSeconds:0}s and {AdNumber} ad(s)");

                        Clear();
                    }

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

        /// <summary>Called when playback crosses a join between two runs of video, after the piece that starts it</summary>
        public static void ObserveDiscontinuity()
        {
            lock (Lock)
            {
                if (!InAd)
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
