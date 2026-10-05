// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public sealed class Tune
    {
        public string What { get; set; }

        public long Milliseconds { get; set; }

        public string SlowestStage { get; set; }

        public long SlowestMilliseconds { get; set; }
    }

    public static class TuneStats
    {
        private const int RecentCount = 10;

        private static readonly object Lock = new object();

        private static readonly List<Tune> Recent = new List<Tune>();

        private static int _count;

        private static long _total;

        private static long _fastest;

        private static long _slowest;

        public static void Add(string what, long milliseconds, string slowestStage, long slowestMilliseconds)
        {
            lock (Lock)
            {
                _fastest = _count == 0 ? milliseconds : Math.Min(_fastest, milliseconds);
                _slowest = Math.Max(_slowest, milliseconds);
                _total += milliseconds;
                _count++;

                Recent.Insert(0, new Tune { What = what, Milliseconds = milliseconds, SlowestStage = slowestStage, SlowestMilliseconds = slowestMilliseconds });

                if (Recent.Count > RecentCount)
                {
                    Recent.RemoveAt(Recent.Count - 1);
                }
            }
        }

        public static (int Count, long Average, long Fastest, long Slowest) Summary()
        {
            lock (Lock)
            {
                return (_count, _count == 0 ? 0 : _total / _count, _fastest, _slowest);
            }
        }

        public static List<Tune> Latest()
        {
            lock (Lock)
            {
                return Recent.ToList();
            }
        }

        public static void Reset()
        {
            lock (Lock)
            {
                Recent.Clear();
                _count = 0;
                _total = 0;
                _fastest = 0;
                _slowest = 0;
            }
        }
    }
}
