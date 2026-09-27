// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    public static class StreamFacts
    {
        private static readonly object Lock = new object();

        private static readonly Regex AudioLine = new Regex(@"^(?:AAC|A/52|E-AC3|MPGA|DTS|MLP|TrueHD|Opus|Vorbis|FLAC)[^:]*channels:\s*(\d+)\s+samplerate:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VideoLine = new Regex(@"original format sz \d+x\d+, of \(\d+,\d+\), vsz (\d+)x(\d+)", RegexOptions.Compiled);

        public static int VideoHeight { get; private set; }

        public static string FrameRate { get; private set; }

        public static int AudioChannels { get; private set; }

        public static int AudioRate { get; private set; }

        public static void Reset()
        {
            lock (Lock)
            {
                VideoHeight = 0;
                FrameRate = null;
                AudioChannels = 0;
                AudioRate = 0;
            }
        }

        public static void Observe(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var audio = AudioLine.Match(message);

            if (audio.Success)
            {
                lock (Lock)
                {
                    AudioChannels = int.Parse(audio.Groups[1].Value, CultureInfo.InvariantCulture);
                    AudioRate = int.Parse(audio.Groups[2].Value, CultureInfo.InvariantCulture);
                }

                return;
            }

            var video = VideoLine.Match(message);

            if (video.Success)
            {
                lock (Lock)
                {
                    VideoHeight = int.Parse(video.Groups[2].Value, CultureInfo.InvariantCulture);
                }
            }
        }

        public static void SetFrameRate(double framesPerSecond)
        {
            if (framesPerSecond <= 0)
            {
                return;
            }

            lock (Lock)
            {
                FrameRate = Math.Ceiling(framesPerSecond).ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
