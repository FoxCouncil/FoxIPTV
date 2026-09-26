// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    /// <summary>A stopwatch over one playback attempt, from the channel change to the first picture, so the slow stage can be named</summary>
    /// <remarks>
    /// Every stage is logged as it happens with its offset from the start, and the first picture triggers one summary line with the time spent between stages.
    /// The latest stage is also exposed as <see cref="Status"/> for the status bar, so the window can say what it is waiting on.
    /// </remarks>
    public static class PlaybackTrace
    {
        private static readonly object Lock = new object();

        private static readonly Stopwatch Clock = new Stopwatch();

        private static readonly List<Tuple<long, string, string>> Marks = new List<Tuple<long, string, string>>();

        private static int _id;

        private static bool _pictureSeen;

        private static string _quality;

        private static readonly HashSet<string> OncePerTrace = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>What playback is doing right now, in a few words</summary>
        public static string Status { get; private set; } = string.Empty;

        /// <summary>Raised on a background thread whenever <see cref="Status"/> changes</summary>
        public static event Action<string> StatusChanged;

        /// <summary>Start timing a new playback attempt</summary>
        /// <param name="what">What is being played, for the log</param>
        public static void Begin(string what)
        {
            lock (Lock)
            {
                _id++;
                _pictureSeen = false;
                _quality = null;
                Marks.Clear();
                OncePerTrace.Clear();
                Clock.Restart();

                TvCore.LogInfo($"[Trace #{_id}] start: {what}");
            }

            SetStatus("Changing channel");
        }

        /// <summary>Record a stage</summary>
        /// <param name="stage">A short name for the stage</param>
        /// <param name="detail">Anything worth keeping next to it</param>
        public static void Mark(string stage, string detail = null)
        {
            long at;
            int id;

            lock (Lock)
            {
                if (!Clock.IsRunning)
                {
                    return;
                }

                at = Clock.ElapsedMilliseconds;
                id = _id;
                Marks.Add(Tuple.Create(at, stage, detail));
            }

            TvCore.LogInfo($"[Trace #{id}] +{at}ms {stage}{(string.IsNullOrEmpty(detail) ? string.Empty : ": " + detail)}");
        }

        /// <summary>Record a stage only the first time it happens in this trace</summary>
        public static void MarkOnce(string stage, string detail = null)
        {
            lock (Lock)
            {
                if (!OncePerTrace.Add(stage))
                {
                    return;
                }
            }

            Mark(stage, detail);
        }

        /// <summary>Record the stream quality LibVLC picked, only when it changes</summary>
        /// <param name="label">Something like "720p 1.5 Mbps"</param>
        public static void Quality(string label)
        {
            lock (Lock)
            {
                if (label == _quality)
                {
                    return;
                }

                _quality = label;
            }

            Mark("quality", label);
        }

        /// <summary>Update the status bar text without adding a stage, for things like buffering percentages</summary>
        public static void SetStatus(string status)
        {
            var changed = false;

            lock (Lock)
            {
                if (Status != status)
                {
                    Status = status;
                    changed = true;
                }
            }

            if (changed)
            {
                StatusChanged?.Invoke(status);
            }
        }

        /// <summary>The first picture is on screen, log where the time went</summary>
        /// <param name="detail">Typically the picture size</param>
        public static void Picture(string detail)
        {
            string summary;
            int id;
            long total;

            lock (Lock)
            {
                if (_pictureSeen || !Clock.IsRunning)
                {
                    return;
                }

                _pictureSeen = true;
                total = Clock.ElapsedMilliseconds;
                id = _id;
                Marks.Add(Tuple.Create(total, "picture", detail));

                var text = new StringBuilder();
                long previous = 0;

                foreach (var mark in Marks)
                {
                    text.Append(mark.Item2).Append(" +").Append(mark.Item1 - previous).Append("ms | ");
                    previous = mark.Item1;
                }

                var slowest = Marks.Select((m, i) => Tuple.Create(m.Item1 - (i == 0 ? 0 : Marks[i - 1].Item1), m.Item2)).OrderByDescending(x => x.Item1).First();

                summary = $"{text}total {total}ms, slowest: {slowest.Item2} ({slowest.Item1}ms)";
            }

            TvCore.LogInfo($"[Trace #{id}] picture after {total}ms: {detail}");
            TvCore.LogInfo($"[Trace #{id}] summary: {summary}");

            SetStatus($"{(_quality ?? detail)}, {total / 1000.0:0.0}s to picture");
        }
    }
}
