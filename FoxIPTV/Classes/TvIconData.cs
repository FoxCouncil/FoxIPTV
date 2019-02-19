// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using Vlc.DotNet.Core.Interops;
    using Vlc.DotNet.Core.Interops.Signatures;

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

        /// <summary>Create a TVIconData factory, mapped from a bool for closed captioning status and a VLC MediaTrack object</summary>
        /// <param name="closedCaptioning">The current Closed Caption state</param>
        /// <param name="mediaTracks">The current VLC MediaTrack object to map values from</param>
        /// <returns>A new instance of a TVIconData mapped from the arguments specified</returns>
        public static TvIconData CreateData(bool closedCaptioning, MediaTrack[] mediaTracks)
        {
            var newObj = new TvIconData { ClosedCaptioning = closedCaptioning };

            // Get VLC's video data
            var videoData = mediaTracks.FirstOrDefault(x => x.Type == MediaTrackTypes.Video);

            if (videoData != null)
            {
                // Get the video codec, converting to FourCC output
                newObj.VideoCodec = string.Format(VIDEO_CODEC, videoData.CodecFourcc.ToFourCC().ToUpper());

                // If the media object has a video track
                if (videoData.TrackInfo is VideoTrack videoTrack)
                {
                    if (videoTrack.Height > 0)
                    {
                        // Get the video height format
                        newObj.VideoSize = string.Format(VIDEO_SIZE, videoTrack.Height);
                    }

                    // These VLC frame rates results can get a little wacky.
                    if (videoTrack.FrameRateNum > 0)
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
            }

            // Get VLC's audio track(s) of type audio or null.
            var audioData = mediaTracks.FirstOrDefault(x => x.Type == MediaTrackTypes.Audio);

            if (audioData == null)
            {
                // Since we don't have any audio data yet, here, take the video data
                return newObj;
            }

            // We can get the Audio codec in FourCC format even if the audio track is not loaded
            newObj.AudioCodec = string.Format(AUDIO_CODEC, audioData.CodecFourcc.ToFourCC().ToUpper());

            if (!(audioData.TrackInfo is AudioTrack audioTrack))
            {
                // Since we don't have any audio track yet, here, take the video data and audio FourCC codec
                return newObj;
            }

            if (audioTrack.Channels > 0)
            {
                var channels = string.Empty;
                
                // More study of how LibVLC exposes this information, but generally there is only two modes we care to show the user
                if (audioTrack.Channels == 2)
                {
                    channels = "STEREO";
                }
                else if (audioTrack.Channels == 6)
                {
                    channels = "SURROUND";
                }

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

            return newObj;
        }
    }
}
