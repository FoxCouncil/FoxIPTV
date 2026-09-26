// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>Tells whether the stream is in an ad break, by watching which pieces LibVLC fetches</summary>
    /// <remarks>
    /// Free ad-supported services splice ads into the same HLS playlist as the programme, and name the ad pieces after the ad. LibVLC logs every piece it fetches, so the names are enough to know when a break starts and ends.
    /// Pluto: "_ad/creative/&lt;id&gt;", bumpers under "Pluto_TV_OandO". Google DAI: pieces from dai.google.com.
    /// Samsung's stitcher, Xumo, Wurl and AWS MediaTailor: ad pieces come from "/v1/segment/" on the stitcher's own host while the programme comes from named origin paths.
    /// Amagi: every ad piece carries "media_type=A" and "break_type=MID_ROLL" in its query string, the piece length in "dur=", and the break length inside its id as "cue-out-120.000000".
    /// Roku names every piece alike and marks breaks only inside the playlist text and in-band SCTE-35, neither of which LibVLC 3 exposes; Roku channels get no readout.
    /// Nothing here opens a connection to anything: the stream plays exactly as the source sends it, and this reads LibVLC's log.
    /// Ad count: Pluto's creative id changes from one ad to the next; everywhere else LibVLC's video demuxer logs one discontinuity restart per ad inside a break.
    /// Time left: only Amagi says how long the break is, so only Amagi gets a countdown.
    /// LibVLC fetches a piece one to eight seconds before it reaches the screen, so the readout leads the picture by that much.
    /// </remarks>
    public static class AdDetector
    {
        /// <summary>Piece address fragments that mark an ad, a bumper or filler</summary>
        private static readonly Regex AdPiece = new Regex(@"_ad(?:/|_bumper)|/creative/|Pluto_TV_OandO|plutotv_filler|dai\.google\.com|/v1/segment/|[?&]media_type=A(?:&|$)|[?&]break_type=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>The ad's own id inside a Pluto piece address, so two pieces of one ad count once and the next ad counts as next</summary>
        private static readonly Regex Creative = new Regex(@"creative/([0-9a-f]{16,})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Amagi's break length, written into every ad piece id</summary>
        private static readonly Regex CueOut = new Regex(@"cue-out-(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Amagi's piece length</summary>
        private static readonly Regex PieceLength = new Regex(@"[?&]dur=(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>LibVLC's video demuxer hitting an EXT-X-DISCONTINUITY: "Restarting demuxer 0 1" is needrestart 0, discontinuity 1; the subtitle stream logs "1 1" and is ignored</summary>
        private const string VideoDiscontinuity = "Restarting demuxer 0 1";

        private static readonly object Lock = new object();

        private static string _lastCreative;

        private static DateTime _breakStarted;

        /// <summary>The discontinuity LibVLC reads on entering the break is the break's own edge, not an ad boundary; it is counted once and skipped</summary>
        private static bool _entryDiscontinuitySeen;

        /// <summary>Amagi: the break length its pieces declare, 0 when unknown</summary>
        private static double _breakSeconds;

        /// <summary>Amagi: seconds of ad pieces fetched since the break began</summary>
        private static double _fetchedSeconds;

        /// <summary>True while the pieces LibVLC is fetching are ads</summary>
        public static bool InAd { get; private set; }

        /// <summary>How many ads this break has held so far, counting the one playing; 0 outside a break</summary>
        public static int AdNumber { get; private set; }

        /// <summary>Seconds of the break left at the piece being fetched, when the source declares the break length; null when it does not</summary>
        public static double? SecondsLeft { get; private set; }

        /// <summary>Forget the current break, a new stream is starting</summary>
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

        /// <summary>Feed every LibVLC log message here; "Retrieving &lt;url&gt;" lines for media pieces and the video demuxer's discontinuity restarts matter</summary>
        public static void Observe(string message)
        {
            if (message == null)
            {
                return;
            }

            if (message.StartsWith(VideoDiscontinuity, StringComparison.Ordinal))
            {
                OnDiscontinuity();

                return;
            }

            if (!message.StartsWith("Retrieving http", StringComparison.Ordinal))
            {
                return;
            }

            var url = Uri.UnescapeDataString(message.Substring("Retrieving ".Length).Trim());
            var path = url.Split('?')[0];

            if (path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".webvtt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".key", StringComparison.OrdinalIgnoreCase))
            {
                // Playlists, subtitles and keys say nothing about what is on screen
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
                    // Pluto names the ad in every piece, so the change of name is the next ad
                    AdNumber++;
                    _lastCreative = creative;

                    TvCore.LogInfo($"[Ads] Next ad in the break, #{AdNumber}: {Short(url)}");
                }

                // Amagi declares the break length and each piece's length, so the time left is known
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

        /// <summary>The video demuxer crossed a discontinuity; inside a break that is the next ad, unless the source already counts ads by name</summary>
        private static void OnDiscontinuity()
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
                    // Pluto: counted by name already
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
