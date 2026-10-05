// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public sealed class AdTally
    {
        public string Channel { get; set; }

        public int Breaks { get; set; }

        public int Ads { get; set; }

        public double Seconds { get; set; }
    }

    public static class AdStats
    {
        private static readonly object Lock = new object();

        private static readonly Dictionary<string, AdTally> Channels = new Dictionary<string, AdTally>(StringComparer.Ordinal);

        private static readonly AdTally All = new AdTally();

        private static bool _inAd;

        private static int _lastNumber;

        public static void Observe(string channel, bool inAd, int adNumber, double elapsed)
        {
            lock (Lock)
            {
                if (!inAd)
                {
                    _inAd = false;
                    _lastNumber = 0;

                    return;
                }

                var tally = Tally(channel ?? string.Empty);

                if (!_inAd)
                {
                    _inAd = true;
                    _lastNumber = Math.Max(1, adNumber);

                    All.Breaks++;
                    All.Ads += _lastNumber;
                    tally.Breaks++;
                    tally.Ads += _lastNumber;
                }
                else if (adNumber > _lastNumber)
                {
                    All.Ads += adNumber - _lastNumber;
                    tally.Ads += adNumber - _lastNumber;
                    _lastNumber = adNumber;
                }

                All.Seconds += elapsed;
                tally.Seconds += elapsed;
            }
        }

        public static AdTally Total()
        {
            lock (Lock)
            {
                return new AdTally { Breaks = All.Breaks, Ads = All.Ads, Seconds = All.Seconds };
            }
        }

        public static List<AdTally> PerChannel()
        {
            lock (Lock)
            {
                return Channels.Values.OrderByDescending(x => x.Seconds).Select(x => new AdTally { Channel = x.Channel, Breaks = x.Breaks, Ads = x.Ads, Seconds = x.Seconds }).ToList();
            }
        }

        public static void Reset()
        {
            lock (Lock)
            {
                Channels.Clear();
                All.Breaks = 0;
                All.Ads = 0;
                All.Seconds = 0;
                _inAd = false;
                _lastNumber = 0;
            }
        }

        private static AdTally Tally(string channel)
        {
            if (!Channels.TryGetValue(channel, out var tally))
            {
                tally = new AdTally { Channel = channel };
                Channels[channel] = tally;
            }

            return tally;
        }
    }
}
