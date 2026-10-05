// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Classes;
    using FFmpeg.AutoGen;

    public unsafe delegate int ReadCallback(byte* buffer, int size);

    public delegate long SeekCallback(long offset, int whence);

    public sealed unsafe class Demuxer : IDisposable
    {
        private const int BufferSize = 64 * 1024;

        private readonly CancellationToken _token;

        private AVFormatContext* _format;

        private AVIOContext* _io;

        private avio_alloc_context_read_packet _read;

        private avio_alloc_context_seek _seek;

        private AVIOInterruptCB_callback _interrupt;

        private Demuxer(CancellationToken token)
        {
            _token = token;
        }

        public int VideoIndex { get; private set; } = -1;

        public int AudioIndex { get; private set; } = -1;

        public HashSet<int> SignalIndices { get; } = new HashSet<int>();

        public string FormatName => _format == null || _format->iformat == null ? "unknown" : Marshal.PtrToStringAnsi((IntPtr)_format->iformat->name);

        public AVStream* Stream(int index) => index < 0 || index >= _format->nb_streams ? null : _format->streams[index];

        public static Demuxer OpenCustom(ReadCallback read, SeekCallback seek, string formatHint, bool probe, CancellationToken token)
        {
            var demuxer = new Demuxer(token);

            try
            {
                demuxer.Open(read, seek, null, formatHint, probe, null);

                return demuxer;
            }
            catch
            {
                demuxer.Dispose();

                throw;
            }
        }

        public static Demuxer OpenUrl(string url, IReadOnlyDictionary<string, string> headers, CancellationToken token)
        {
            var demuxer = new Demuxer(token);

            try
            {
                demuxer.Open(null, null, url, null, true, headers);

                return demuxer;
            }
            catch
            {
                demuxer.Dispose();

                throw;
            }
        }

        private void Open(ReadCallback read, SeekCallback seek, string url, string formatHint, bool probe, IReadOnlyDictionary<string, string> headers)
        {
            _format = ffmpeg.avformat_alloc_context();

            _interrupt = opaque => _token.IsCancellationRequested ? 1 : 0;
            _format->interrupt_callback.callback = _interrupt;

            if (read != null)
            {
                var buffer = (byte*)ffmpeg.av_malloc(BufferSize);

                _read = (opaque, data, size) => read(data, size);
                _seek = seek == null ? null : (avio_alloc_context_seek)((opaque, offset, whence) => seek(offset, whence));

                _io = ffmpeg.avio_alloc_context(buffer, BufferSize, 0, null, _read, null, _seek);

                if (_io == null)
                {
                    ffmpeg.av_free(buffer);

                    throw new PlayerException("avio_alloc_context failed");
                }

                _io->seekable = seek == null ? 0 : ffmpeg.AVIO_SEEKABLE_NORMAL;

                _format->pb = _io;
                _format->flags |= ffmpeg.AVFMT_FLAG_CUSTOM_IO;
            }

            _format->flags |= ffmpeg.AVFMT_FLAG_DISCARD_CORRUPT;

            AVDictionary* options = null;

            ffmpeg.av_dict_set(&options, "probesize", probe ? "2000000" : "1000000", 0);
            ffmpeg.av_dict_set(&options, "analyzeduration", probe ? "1500000" : "1000000", 0);
            ffmpeg.av_dict_set(&options, "fpsprobesize", "0", 0);
            ffmpeg.av_dict_set(&options, "merge_pmt_versions", "1", 0);

            if (url != null)
            {
                ffmpeg.av_dict_set(&options, "rw_timeout", "15000000", 0);
                ffmpeg.av_dict_set(&options, "rtsp_transport", "tcp", 0);
                ffmpeg.av_dict_set(&options, "user_agent", headers != null && headers.TryGetValue("User-Agent", out var agent) ? agent : Web.UserAgent, 0);
            }

            AVInputFormat* format = null;

            if (!string.IsNullOrEmpty(formatHint))
            {
                format = ffmpeg.av_find_input_format(formatHint);
            }

            var formatContext = _format;
            var opened = ffmpeg.avformat_open_input(&formatContext, url, format, &options);

            _format = formatContext;

            ffmpeg.av_dict_free(&options);

            FFmpegNative.Check(opened, "Opening the stream");

            if (probe)
            {
                var found = ffmpeg.avformat_find_stream_info(_format, null);

                if (found < 0)
                {
                    TvCore.LogError($"[Player] Stream details incomplete: {FFmpegNative.Error(found)}");
                }
            }

            SelectStreams();
        }

        public void SelectStreams()
        {
            var video = ffmpeg.av_find_best_stream(_format, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);

            if (video >= 0 && (_format->streams[video]->disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) != 0)
            {
                video = -1;
            }

            var audio = ffmpeg.av_find_best_stream(_format, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, video, null, 0);

            if (audio < 0)
            {
                audio = FirstOfType(AVMediaType.AVMEDIA_TYPE_AUDIO);
            }

            if (video < 0)
            {
                video = FirstOfType(AVMediaType.AVMEDIA_TYPE_VIDEO);
            }

            VideoIndex = video >= 0 ? video : -1;
            AudioIndex = audio >= 0 ? audio : -1;

            SignalIndices.Clear();

            for (var i = 0; i < _format->nb_streams; i++)
            {
                var codec = _format->streams[i]->codecpar->codec_id;

                if (codec == AVCodecID.AV_CODEC_ID_SCTE_35 || codec == AVCodecID.AV_CODEC_ID_TIMED_ID3)
                {
                    SignalIndices.Add(i);
                }

                _format->streams[i]->discard = i == VideoIndex || i == AudioIndex || SignalIndices.Contains(i) ? AVDiscard.AVDISCARD_DEFAULT : AVDiscard.AVDISCARD_ALL;
            }
        }

        private int FirstOfType(AVMediaType type)
        {
            for (var i = 0; i < _format->nb_streams; i++)
            {
                var stream = _format->streams[i];

                if (stream->codecpar->codec_type == type && stream->codecpar->codec_id != AVCodecID.AV_CODEC_ID_NONE && (stream->disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        public string Describe()
        {
            var parts = new List<string>();

            for (var i = 0; i < _format->nb_streams; i++)
            {
                var stream = _format->streams[i];
                var parameters = stream->codecpar;
                var name = ffmpeg.avcodec_get_name(parameters->codec_id);
                var chosen = i == VideoIndex || i == AudioIndex ? "*" : string.Empty;

                switch (parameters->codec_type)
                {
                    case AVMediaType.AVMEDIA_TYPE_VIDEO:
                    {
                        parts.Add($"{chosen}#{i} video {name} {parameters->width}x{parameters->height}");
                    }
                    break;

                    case AVMediaType.AVMEDIA_TYPE_AUDIO:
                    {
                        parts.Add($"{chosen}#{i} audio {name} {parameters->ch_layout.nb_channels}ch {parameters->sample_rate}Hz");
                    }
                    break;

                    default:
                    {
                        parts.Add($"#{i} {parameters->codec_type.ToString().Replace("AVMEDIA_TYPE_", string.Empty).ToLowerInvariant()} {name}");
                    }
                    break;
                }
            }

            return $"{FormatName}: {string.Join(", ", parts)}";
        }

        public int Read(AVPacket* packet)
        {
            return ffmpeg.av_read_frame(_format, packet);
        }

        public void Dispose()
        {
            if (_format != null)
            {
                var format = _format;

                ffmpeg.avformat_close_input(&format);

                _format = null;
            }

            if (_io != null)
            {
                ffmpeg.av_freep(&_io->buffer);

                var io = _io;

                ffmpeg.avio_context_free(&io);

                _io = null;
            }
        }
    }
}
