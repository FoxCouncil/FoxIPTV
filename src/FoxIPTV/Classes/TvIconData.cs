// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using Playback;

    /// <summary>
    /// A class to contain the data state of various icons to inform
    /// the user of technical stream details.
    /// </summary>
    public class TvIconData : IEquatable<TvIconData>
    {
        // Constants to format the data to icon resource keys
        private const string VIDEO_CODEC = "VC_{0}";
        private const string VIDEO_SIZE = "VS_{0}P";
        private const string FRAME_RATE = "FR_{0}FPS";
        private const string AUDIO_CODEC = "AC_{0}";
        private const string AUDIO_CHANNELS = "CH_{0}";
        private const string AUDIO_RATE = "AR_{0}KHZ";

        /// <summary>Show or hide an icon if Closed Captioning information is available</summary>
        public bool ClosedCaptioning { get; set; }

        /// <summary>Icon key of the current video codec, ie: H264, HEVC</summary>
        public string VideoCodec { get; set; }

        /// <summary>Icon key of the current video height in uppercase P format, ie: 720P, 1080P</summary>
        public string VideoSize { get; set; }

        /// <summary>Icon key of the current video frame rate, suffixed with capitals FPS, ie: 25FPS, 30FPS</summary>
        public string FrameRate { get; set; }

        /// <summary>Icon key of the current audio codec, ie: AAC, AC3</summary>
        public string AudioCodec { get; set; }

        /// <summary>Icon key of the current audio channel, ie: STEREO, 5.1</summary>
        public string AudioChannel { get; set; }

        /// <summary>Icon key of the current audio sample rate, ie: 44KHZ, 48KHZ</summary>
        public string AudioRate { get; set; }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((TvIconData) obj);
        }

        /// <inheritdoc />
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ClosedCaptioning.GetHashCode();
                hashCode = (hashCode * 397) ^ (VideoCodec != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(VideoCodec) : 0);
                hashCode = (hashCode * 397) ^ (VideoSize != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(VideoSize) : 0);
                hashCode = (hashCode * 397) ^ (FrameRate != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(FrameRate) : 0);
                hashCode = (hashCode * 397) ^ (AudioCodec != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(AudioCodec) : 0);
                hashCode = (hashCode * 397) ^ (AudioChannel != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(AudioChannel) : 0);
                hashCode = (hashCode * 397) ^ (AudioRate != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(AudioRate) : 0);
                return hashCode;
            }
        }

        public static bool operator ==(TvIconData left, TvIconData right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TvIconData left, TvIconData right)
        {
            return !Equals(left, right);
        }

        /// <inheritdoc />
        public bool Equals(TvIconData other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ClosedCaptioning == other.ClosedCaptioning && 
                   string.Equals(VideoCodec, other.VideoCodec, StringComparison.OrdinalIgnoreCase) && 
                   string.Equals(VideoSize, other.VideoSize, StringComparison.OrdinalIgnoreCase) && 
                   string.Equals(FrameRate, other.FrameRate, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(AudioCodec, other.AudioCodec, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(AudioChannel, other.AudioChannel, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(AudioRate, other.AudioRate, StringComparison.OrdinalIgnoreCase);
        }

        private static string CodecName(string codec)
        {
            switch ((codec ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "h264":
                {
                    return "H264";
                }

                case "hevc":
                {
                    return "HEVC";
                }

                case "mpeg2video":
                {
                    return "MPEG2";
                }

                case "mpeg1video":
                {
                    return "MPEG1";
                }

                case "mpeg4":
                {
                    return "MPEG4";
                }

                case "mp2":
                case "mp2float":
                {
                    return "MP2";
                }

                case "mp3":
                case "mp3float":
                {
                    return "MP3";
                }

                case "aac_latm":
                {
                    return "AAC";
                }

                case "dca":
                {
                    return "DTS";
                }

                default:
                {
                    return (codec ?? string.Empty).Trim().ToUpperInvariant();
                }
            }
        }

        private static string ChannelName(int channels)
        {
            switch (channels)
            {
                case 1:
                {
                    return "MONO";
                }

                case 2:
                {
                    return "STEREO";
                }

                case 6:
                {
                    return "SURROUND";
                }

                case 8:
                {
                    return "7.1";
                }

                default:
                {
                    return $"{channels}CH";
                }
            }
        }

        /// <returns>A new instance of a TVIconData mapped from what the player knows about the stream</returns>
        public static TvIconData CreateData(StreamInfo info)
        {
            var data = new TvIconData { ClosedCaptioning = info?.Captions ?? false };

            if (info == null)
            {
                return data;
            }

            if (!string.IsNullOrEmpty(info.VideoCodec))
            {
                data.VideoCodec = string.Format(VIDEO_CODEC, CodecName(info.VideoCodec));
            }

            if (info.Height > 0)
            {
                data.VideoSize = string.Format(VIDEO_SIZE, info.Height);
            }

            if (info.FrameRate > 0)
            {
                data.FrameRate = string.Format(FRAME_RATE, Math.Min(90, Math.Ceiling(info.FrameRate)).ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrEmpty(info.AudioCodec))
            {
                data.AudioCodec = string.Format(AUDIO_CODEC, CodecName(info.AudioCodec));
            }

            if (info.AudioChannels > 0)
            {
                data.AudioChannel = string.Format(AUDIO_CHANNELS, ChannelName(info.AudioChannels));
            }

            if (info.AudioRate > 0)
            {
                data.AudioRate = string.Format(AUDIO_RATE, Math.Floor(info.AudioRate / 1000m));
            }

            return data;
        }
    }
}
