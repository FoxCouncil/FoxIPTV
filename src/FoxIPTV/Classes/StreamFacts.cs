// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>What we have learned about the stream now playing from sources other than LibVLC's track list</summary>
    /// <remarks>
    /// LibVLC's track list is filled by the demuxer, and for the transport streams these services use it leaves the frame rate, the audio channel count and the sample rate at zero.
    /// The decoders and packetizers do know, and they say so in the log, so those lines are read here. The HLS master playlist carries the frame rate when the service bothers to write it.
    /// </remarks>
    public static class StreamFacts
    {
        private static readonly object Lock = new object();

        private static readonly Regex AudioLine = new Regex(@"^(?:AAC|A/52|E-AC3|MPGA|DTS|MLP|TrueHD|Opus|Vorbis|FLAC)[^:]*channels:\s*(\d+)\s+samplerate:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VideoLine = new Regex(@"original format sz \d+x\d+, of \(\d+,\d+\), vsz (\d+)x(\d+)", RegexOptions.Compiled);

        /// <summary>The visible picture height in lines, 0 when unknown</summary>
        public static int VideoHeight { get; private set; }

        /// <summary>The frame rate rounded up to whole frames, null when unknown</summary>
        public static string FrameRate { get; private set; }

        /// <summary>The audio channel count, 0 when unknown</summary>
        public static int AudioChannels { get; private set; }

        /// <summary>The audio sample rate in Hz, 0 when unknown</summary>
        public static int AudioRate { get; private set; }

        /// <summary>Forget everything, a new stream is starting</summary>
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

        /// <summary>Read one LibVLC log line for anything useful</summary>
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

        /// <summary>The playlist said how many frames per second the picture has</summary>
        /// <param name="framesPerSecond">The FRAME-RATE attribute value</param>
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
