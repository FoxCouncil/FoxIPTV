// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Collections.Generic;

    public enum PlayerState
    {
        Idle,
        Opening,
        Buffering,
        Playing,
        Ended,
        Failed,
        Protected
    }

    public enum StereoMode
    {
        Stereo = 1,
        ReverseStereo = 2,
        Left = 3,
        Right = 4,
        Surround = 5,
        Mono = 7
    }

    public sealed class MediaRequest
    {
        public Uri Uri { get; set; }

        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool IsLive { get; set; } = true;

        public string Label { get; set; }

        public override string ToString() => Label ?? Uri?.ToString();
    }

    public sealed class StreamInfo : IEquatable<StreamInfo>
    {
        public string VideoCodec { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public double FrameRate { get; set; }

        public bool Interlaced { get; set; }

        public bool HardwareDecoding { get; set; }

        public string AudioCodec { get; set; }

        public int AudioChannels { get; set; }

        public int AudioRate { get; set; }

        public bool Captions { get; set; }

        public StreamInfo Clone() => (StreamInfo)MemberwiseClone();

        public bool Equals(StreamInfo other)
        {
            return other != null && VideoCodec == other.VideoCodec && Width == other.Width && Height == other.Height && Math.Abs(FrameRate - other.FrameRate) < 0.01 && Interlaced == other.Interlaced && HardwareDecoding == other.HardwareDecoding && AudioCodec == other.AudioCodec && AudioChannels == other.AudioChannels && AudioRate == other.AudioRate && Captions == other.Captions;
        }

        public override bool Equals(object obj) => Equals(obj as StreamInfo);

        public override int GetHashCode() => HashCode.Combine(VideoCodec, Width, Height, AudioCodec, AudioChannels, AudioRate, Captions);

        public override string ToString() => $"video {VideoCodec ?? "none"} {Width}x{Height} {FrameRate:0.##}fps{(Interlaced ? " interlaced" : string.Empty)}{(HardwareDecoding ? " (GPU)" : string.Empty)}, audio {AudioCodec ?? "none"} {AudioChannels}ch {AudioRate}Hz{(Captions ? ", captions" : string.Empty)}";
    }

    public sealed class PlayerStats
    {
        public double BufferedSeconds { get; set; }

        public double BufferTargetSeconds { get; set; }

        public long BytesLoaded { get; set; }

        public int PiecesLoaded { get; set; }

        public double LastPieceAgo { get; set; } = -1;

        public double LastPlaylistAgo { get; set; } = -1;

        public long FramesDecoded { get; set; }

        public long FramesShown { get; set; }

        public long FramesDropped { get; set; }

        public long AudioUnderruns { get; set; }

        public double Clock { get; set; }

        public string Variant { get; set; }

        public override string ToString() => $"buffer {BufferedSeconds:0.0}s of {BufferTargetSeconds:0.#}s, {PiecesLoaded} pieces {BytesLoaded / 1048576.0:0.0}MB, last piece {(LastPieceAgo < 0 ? "never" : $"{LastPieceAgo:0.0}s ago")}, last playlist {(LastPlaylistAgo < 0 ? "never" : $"{LastPlaylistAgo:0.0}s ago")}, video {FramesDecoded} decoded {FramesShown} shown {FramesDropped} dropped, audio {AudioUnderruns} underruns, clock {Clock:0.000}{(Variant == null ? string.Empty : $", variant {Variant}")}";
    }
}
