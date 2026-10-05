// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Hls;

    public sealed class SubtitleTrack
    {
        public const double WrapSeconds = 8589934592 / WebVtt.MpegTsHz;

        private const double Reach = 2.0;

        private const double WaitSeconds = 60;

        private const double KeepSeconds = 1.0;

        private const int MaxRuns = 64;

        private const int MaxCues = 2000;

        private readonly object _lock = new object();

        private readonly List<Run> _runs = new List<Run>();

        private readonly List<Waiting> _waiting = new List<Waiting>();

        private readonly List<Placed> _placed = new List<Placed>();

        private readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);

        private static double Now => Environment.TickCount64 / 1000.0;

        public void NoteVideo(int discontinuity, double source, double mapped)
        {
            var offset = mapped - source;

            lock (_lock)
            {
                var last = _runs.Count > 0 ? _runs[_runs.Count - 1] : null;

                if (last != null && last.Discontinuity == discontinuity && Math.Abs(last.Offset - offset) < 0.001)
                {
                    last.From = Math.Min(last.From, source);
                    last.To = Math.Max(last.To, source);

                    return;
                }

                _runs.Add(new Run { Discontinuity = discontinuity, Offset = offset, From = source, To = source });

                if (_runs.Count > MaxRuns)
                {
                    _runs.RemoveAt(0);
                }
            }
        }

        public void Add(int discontinuity, IEnumerable<WebVttCue> cues)
        {
            lock (_lock)
            {
                foreach (var cue in cues)
                {
                    var key = $"{discontinuity}|{cue.Start:0.000}|{cue.End:0.000}|{cue.Text}";

                    if (_waiting.Count + _placed.Count >= MaxCues || !_known.Add(key))
                    {
                        continue;
                    }

                    _waiting.Add(new Waiting { Key = key, Discontinuity = discontinuity, Cue = cue, Since = Now });
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _waiting.Clear();
                _placed.Clear();
                _known.Clear();
            }
        }

        public string TextAt(double clock)
        {
            lock (_lock)
            {
                Place();

                for (var i = _placed.Count - 1; i >= 0; i--)
                {
                    if (_placed[i].End < clock - KeepSeconds)
                    {
                        _known.Remove(_placed[i].Key);
                        _placed.RemoveAt(i);
                    }
                }

                return string.Join("\n", _placed.Where(x => x.Start <= clock && clock < x.End).OrderBy(x => x.Start).Select(x => x.Text));
            }
        }

        private void Place()
        {
            var now = Now;

            for (var i = 0; i < _waiting.Count; i++)
            {
                var item = _waiting[i];
                var outcome = Locate(item.Discontinuity, item.Cue.Start, out var offset);

                if (outcome == Outcome.Wait && now - item.Since < WaitSeconds)
                {
                    continue;
                }

                _waiting.RemoveAt(i--);

                if (outcome != Outcome.Placed)
                {
                    _known.Remove(item.Key);

                    continue;
                }

                _placed.Add(new Placed { Key = item.Key, Start = item.Cue.Start + offset, End = item.Cue.End + offset, Text = item.Cue.Text });
            }
        }

        private Outcome Locate(int discontinuity, double source, out double offset)
        {
            offset = 0;

            Run latest = null;
            Run earliest = null;

            for (var i = _runs.Count - 1; i >= 0; i--)
            {
                var run = _runs[i];

                if (run.Discontinuity != discontinuity)
                {
                    continue;
                }

                var shift = Shift(source, run.To);

                if (source + shift >= run.From - Reach && source + shift <= run.To + Reach)
                {
                    offset = run.Offset + shift;

                    return Outcome.Placed;
                }

                latest ??= run;
                earliest = run;
            }

            if (latest == null)
            {
                return _runs.Count > 0 && _runs[_runs.Count - 1].Discontinuity > discontinuity ? Outcome.Drop : Outcome.Wait;
            }

            var ahead = source + Shift(source, latest.To) > latest.To;

            if (ahead && ReferenceEquals(latest, _runs[_runs.Count - 1]))
            {
                return Outcome.Wait;
            }

            var nearest = ahead ? latest : earliest;

            offset = nearest.Offset + Shift(source, nearest.To);

            return Outcome.Placed;
        }

        private static double Shift(double source, double reference)
        {
            return WrapSeconds * Math.Round((reference - source) / WrapSeconds);
        }

        private enum Outcome
        {
            Placed,
            Wait,
            Drop
        }

        private sealed class Run
        {
            public int Discontinuity { get; set; }

            public double Offset { get; set; }

            public double From { get; set; }

            public double To { get; set; }
        }

        private sealed class Waiting
        {
            public string Key { get; set; }

            public int Discontinuity { get; set; }

            public WebVttCue Cue { get; set; }

            public double Since { get; set; }
        }

        private sealed class Placed
        {
            public string Key { get; set; }

            public double Start { get; set; }

            public double End { get; set; }

            public string Text { get; set; }
        }
    }
}
