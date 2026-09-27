// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using LibVLCSharp.Shared;

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

        /// <summary>Icon key of the current video codec in FourCC format, ie: H264, etc</summary>
        public string VideoCodec { get; set; }

        /// <summary>Icon key of the current video height in uppercase P format, ie: 720P, 1080P</summary>
        public string VideoSize { get; set; }

        /// <summary>Icon key of the current video frame rate, suffixed with capitals FPS, ie: 25FPS, 30FPS</summary>
        public string FrameRate { get; set; }

        /// <summary>Icon key of the current audio codec in FourCC format, ie: M4A, AC3</summary>
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

        private static string CodecName(string fourCc)
        {
            switch ((fourCc ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "h264":
                case "avc1":
                {
                    return "H264";
                }

                case "hevc":
                case "hvc1":
                case "hev1":
                case "h265":
                {
                    return "HEVC";
                }

                case "av01":
                {
                    return "AV1";
                }

                case "vp09":
                case "vp90":
                {
                    return "VP9";
                }

                case "mpgv":
                case "mp2v":
                {
                    return "MPEG2";
                }

                case "a52":
                case "a52 ":
                case "ac-3":
                {
                    return "AC3";
                }

                case "eac3":
                {
                    return "EAC3";
                }

                default:
                {
                    return (fourCc ?? string.Empty).Trim().ToUpperInvariant();
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

        private static TvIconData ApplyFacts(TvIconData data)
        {
            if (data.VideoSize == null && StreamFacts.VideoHeight > 0)
            {
                data.VideoSize = string.Format(VIDEO_SIZE, StreamFacts.VideoHeight);
            }

            if (data.FrameRate == null && StreamFacts.FrameRate != null)
            {
                data.FrameRate = string.Format(FRAME_RATE, StreamFacts.FrameRate);
            }

            if (data.AudioChannel == null && StreamFacts.AudioChannels > 0)
            {
                data.AudioChannel = string.Format(AUDIO_CHANNELS, ChannelName(StreamFacts.AudioChannels));
            }

            if (data.AudioRate == null && StreamFacts.AudioRate > 0)
            {
                data.AudioRate = string.Format(AUDIO_RATE, Math.Floor(StreamFacts.AudioRate / 1000m));
            }

            return data;
        }

        /// <param name="closedCaptioning">The current Closed Caption state</param>
        /// <returns>A new instance of a TVIconData mapped from the arguments specified</returns>
        public static TvIconData CreateData(bool closedCaptioning, MediaTrack[] mediaTracks)
        {
            var newObj = new TvIconData { ClosedCaptioning = closedCaptioning };

            // Get VLC's video data
            var videoTracks = mediaTracks.Where(x => x.TrackType == TrackType.Video).ToArray();

            if (videoTracks.Length > 0)
            {
                var videoData = videoTracks[0];

                // Get the video codec, converting to FourCC output
                newObj.VideoCodec = string.Format(VIDEO_CODEC, CodecName(videoData.Codec.ToFourCC()));

                var videoTrack = videoData.Data.Video;

                if (videoTrack.Height > 0)
                {
                    // Get the video height format
                    newObj.VideoSize = string.Format(VIDEO_SIZE, videoTrack.Height);
                }

                // These VLC frame rates results can get a little wacky.
                if (videoTrack.FrameRateNum > 0 && videoTrack.FrameRateDen > 0)
                {
                    var frameRateStr = Math.Ceiling(videoTrack.FrameRateNum / (double)videoTrack.FrameRateDen).ToString(CultureInfo.InvariantCulture);

                    if (videoTrack.FrameRateNum > 90 && videoTrack.FrameRateDen == 1)
                    {
                        // Woah, too fast, default to 90FPS
                        frameRateStr = "90";
                    }

                    newObj.FrameRate = string.Format(FRAME_RATE, frameRateStr);
                }
            }

            // Get VLC's audio track(s) of type audio or null.
            var audioTracks = mediaTracks.Where(x => x.TrackType == TrackType.Audio).ToArray();

            if (audioTracks.Length == 0)
            {
                // Since we don't have any audio data yet, here, take the video data
                return ApplyFacts(newObj);
            }

            var audioData = audioTracks[0];

            // We can get the Audio codec in FourCC format even if the audio track is not loaded
            newObj.AudioCodec = string.Format(AUDIO_CODEC, CodecName(audioData.Codec.ToFourCC()));

            var audioTrack = audioData.Data.Audio;

            if (audioTrack.Channels > 0)
            {
                // More study of how LibVLC exposes this information, but generally there is only two modes we care to show the user
                var channels = ChannelName((int)audioTrack.Channels);

                if (channels != string.Empty)
                {
                    newObj.AudioChannel = string.Format(AUDIO_CHANNELS, channels);
                }
            }

            if (audioTrack.Rate > 0)
            {
                // Solid audio rate
                newObj.AudioRate = string.Format(AUDIO_RATE, Math.Floor((decimal) audioTrack.Rate / 1000));
            }

            return ApplyFacts(newObj);
        }
    }
}
