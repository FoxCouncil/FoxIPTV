// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using Vlc.DotNet.Core.Interops;
    using Vlc.DotNet.Core.Interops.Signatures;

    public class TvIconData : IEquatable<TvIconData>
    {
        private const string VIDEO_CODEC = "VC_{0}";
        private const string VIDEO_SIZE = "VS_{0}P";
        private const string FRAME_RATE = "FR_{0}FPS";
        private const string AUDIO_CODEC = "AC_{0}";
        private const string AUDIO_CHANNELS = "CH_{0}";
        private const string AUDIO_RATE = "AR_{0}KHZ";

        public bool ClosedCaptioning { get; set; }

        public string VideoCodec { get; set; }

        public string VideoSize { get; set; }

        public string FrameRate { get; set; }

        public string AudioCodec { get; set; }

        public string AudioChannel { get; set; }

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

        public static TvIconData CreateData(bool closedCaptioning, MediaTrack[] mediaTracks)
        {
            var newObj = new TvIconData { ClosedCaptioning = closedCaptioning };

            var videoData = mediaTracks.FirstOrDefault(x => x.Type == MediaTrackTypes.Video);

            if (videoData != null)
            {
                newObj.VideoCodec = string.Format(VIDEO_CODEC, videoData.CodecFourcc.ToFourCC().ToUpper());

                if (videoData.TrackInfo is VideoTrack videoTrack)
                {
                    if (videoTrack.Height > 0)
                    {
                        newObj.VideoSize = string.Format(VIDEO_SIZE, videoTrack.Height);
                    }

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

            var audioData = mediaTracks.FirstOrDefault(x => x.Type == MediaTrackTypes.Audio);

            if (audioData == null)
            {
                return newObj;
            }

            newObj.AudioCodec = string.Format(AUDIO_CODEC, audioData.CodecFourcc.ToFourCC().ToUpper());

            if (audioData.TrackInfo is AudioTrack audioTrack)
            {
                if (audioTrack.Channels > 0)
                {
                    var channels = string.Empty;

                    switch (audioTrack.Channels)
                    {
                        case 2:
                        {
                            channels = "STEREO";
                        }
                            break;

                        case 6:
                        {
                            channels = "SURROUND";
                        }
                            break;
                    }

                    if (channels != string.Empty)
                    {
                        newObj.AudioChannel = string.Format(AUDIO_CHANNELS, channels);
                    }
                }

                if (audioTrack.Rate > 0)
                {
                    newObj.AudioRate = string.Format(AUDIO_RATE, Math.Floor((decimal) audioTrack.Rate / 1000));
                }
            }

            return newObj;
        }
    }
}
