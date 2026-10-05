// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;
    using Classes;
    using FFmpeg.AutoGen;

    public sealed unsafe class HardwareDevice : IDisposable
    {
        private AVBufferRef* _reference;

        private HardwareDevice(AVBufferRef* reference, AVHWDeviceType type, AVPixelFormat format, string name)
        {
            _reference = reference;

            Type = type;
            PixelFormat = format;
            Name = name;
        }

        public AVBufferRef* Reference => _reference;

        public AVHWDeviceType Type { get; }

        public AVPixelFormat PixelFormat { get; }

        public string Name { get; }

        public static HardwareDevice FromD3D11(IntPtr device)
        {
            var reference = ffmpeg.av_hwdevice_ctx_alloc(AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA);

            if (reference == null)
            {
                return null;
            }

            var context = (AVHWDeviceContext*)reference->data;
            var d3d11 = (AVD3D11VADeviceContext*)context->hwctx;

            Marshal.AddRef(device);

            d3d11->device = (ID3D11Device*)device;

            var result = ffmpeg.av_hwdevice_ctx_init(reference);

            if (result < 0)
            {
                TvCore.LogError($"[Player] D3D11VA device setup failed: {FFmpegNative.Error(result)}");

                ffmpeg.av_buffer_unref(&reference);

                return null;
            }

            return new HardwareDevice(reference, AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA, AVPixelFormat.AV_PIX_FMT_D3D11, "D3D11VA");
        }

        public static HardwareDevice Create(AVHWDeviceType type)
        {
            AVBufferRef* reference = null;

            var result = ffmpeg.av_hwdevice_ctx_create(&reference, type, null, null, 0);

            if (result < 0)
            {
                TvCore.LogInfo($"[Player] No {type} decoding on this machine: {FFmpegNative.Error(result)}");

                return null;
            }

            switch (type)
            {
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX:
                {
                    return new HardwareDevice(reference, type, AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX, "VideoToolbox");
                }

                case AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI:
                {
                    return new HardwareDevice(reference, type, AVPixelFormat.AV_PIX_FMT_VAAPI, "VAAPI");
                }

                default:
                {
                    ffmpeg.av_buffer_unref(&reference);

                    return null;
                }
            }
        }

        public HardwareDevice Share()
        {
            if (_reference == null)
            {
                return null;
            }

            var reference = ffmpeg.av_buffer_ref(_reference);

            return reference == null ? null : new HardwareDevice(reference, Type, PixelFormat, Name);
        }

        public void Dispose()
        {
            if (_reference != null)
            {
                var reference = _reference;

                ffmpeg.av_buffer_unref(&reference);

                _reference = null;
            }
        }
    }

    public abstract unsafe class Decoder : IDisposable
    {
        protected AVCodecContext* Context;

        public string CodecName => Context == null ? null : ffmpeg.avcodec_get_name(Context->codec_id);

        public AVCodecContext* Codec => Context;

        public int Send(AVPacket* packet)
        {
            return ffmpeg.avcodec_send_packet(Context, packet);
        }

        public int Receive(AVFrame* frame)
        {
            return ffmpeg.avcodec_receive_frame(Context, frame);
        }

        public void Flush()
        {
            ffmpeg.avcodec_flush_buffers(Context);
        }

        protected static AVCodec* Find(AVCodecParameters* parameters)
        {
            var codec = ffmpeg.avcodec_find_decoder(parameters->codec_id);

            if (codec == null)
            {
                throw new PlayerException($"No decoder for {ffmpeg.avcodec_get_name(parameters->codec_id)}");
            }

            return codec;
        }

        protected static AVCodecContext* Allocate(AVCodec* codec, AVCodecParameters* parameters)
        {
            var context = ffmpeg.avcodec_alloc_context3(codec);

            FFmpegNative.Check(ffmpeg.avcodec_parameters_to_context(context, parameters), "Copying stream details");

            context->pkt_timebase = new AVRational { num = 1, den = ffmpeg.AV_TIME_BASE };

            return context;
        }

        protected void OpenCodec(AVCodec* codec, AVCodecParameters* parameters)
        {
            var opened = ffmpeg.avcodec_open2(Context, codec, null);

            if (opened < 0)
            {
                Dispose();

                FFmpegNative.Check(opened, $"Opening the {ffmpeg.avcodec_get_name(parameters->codec_id)} decoder");
            }
        }

        public virtual void Dispose()
        {
            if (Context != null)
            {
                var context = Context;

                ffmpeg.avcodec_free_context(&context);

                Context = null;
            }
        }
    }

    public sealed unsafe class VideoDecoder : Decoder
    {
        private const int HardwareConfigDeviceContext = 0x01;

        private AVCodecContext_get_format _getFormat;

        private AVPixelFormat _hardwareFormat = AVPixelFormat.AV_PIX_FMT_NONE;

        private bool _loggedFallback;

        public bool UsesHardware { get; private set; }

        public string HardwareName { get; private set; }

        public static VideoDecoder Open(AVCodecParameters* parameters, HardwareDevice hardware)
        {
            var codec = Find(parameters);
            var decoder = new VideoDecoder();

            decoder.Context = Allocate(codec, parameters);

            if (hardware != null && Supports(codec, hardware.Type))
            {
                decoder._hardwareFormat = hardware.PixelFormat;
                decoder._getFormat = decoder.PickFormat;

                decoder.Context->hw_device_ctx = ffmpeg.av_buffer_ref(hardware.Reference);
                decoder.Context->get_format = decoder._getFormat;
                decoder.Context->thread_count = 1;

                if (hardware.Type == AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA)
                {
                    decoder.Context->extra_hw_frames = 12;
                }

                decoder.UsesHardware = true;
                decoder.HardwareName = hardware.Name;
            }
            else
            {
                decoder.Context->thread_count = 0;
                decoder.Context->thread_type = ffmpeg.FF_THREAD_FRAME | ffmpeg.FF_THREAD_SLICE;
            }

            decoder.OpenCodec(codec, parameters);

            return decoder;
        }

        private static bool Supports(AVCodec* codec, AVHWDeviceType type)
        {
            for (var i = 0; ; i++)
            {
                var config = ffmpeg.avcodec_get_hw_config(codec, i);

                if (config == null)
                {
                    return false;
                }

                if ((config->methods & HardwareConfigDeviceContext) != 0 && config->device_type == type)
                {
                    return true;
                }
            }
        }

        private AVPixelFormat PickFormat(AVCodecContext* context, AVPixelFormat* formats)
        {
            for (var format = formats; *format != AVPixelFormat.AV_PIX_FMT_NONE; format++)
            {
                if (*format == _hardwareFormat)
                {
                    return *format;
                }
            }

            if (!_loggedFallback)
            {
                _loggedFallback = true;

                TvCore.LogInfo($"[Player] {HardwareName} can't take this {CodecName} stream (profile {context->profile}), decoding on the CPU");
            }

            UsesHardware = false;

            return ffmpeg.avcodec_default_get_format(context, formats);
        }
    }

    public sealed unsafe class AudioDecoder : Decoder
    {
        public static AudioDecoder Open(AVCodecParameters* parameters)
        {
            var codec = Find(parameters);
            var decoder = new AudioDecoder();

            decoder.Context = Allocate(codec, parameters);
            decoder.Context->thread_count = 1;

            decoder.OpenCodec(codec, parameters);

            return decoder;
        }
    }

    public sealed unsafe class CaptionDecoder : IDisposable
    {
        private AVCodecContext* _context;

        private AVPacket* _packet;

        public static CaptionDecoder Open(int field)
        {
            var codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_EIA_608);

            if (codec == null)
            {
                return null;
            }

            var decoder = new CaptionDecoder
            {
                _context = ffmpeg.avcodec_alloc_context3(codec),
                _packet = ffmpeg.av_packet_alloc()
            };

            decoder._context->pkt_timebase = new AVRational { num = 1, den = ffmpeg.AV_TIME_BASE };

            ffmpeg.av_opt_set_int(decoder._context, "real_time", 1, ffmpeg.AV_OPT_SEARCH_CHILDREN);
            ffmpeg.av_opt_set_int(decoder._context, "data_field", field, ffmpeg.AV_OPT_SEARCH_CHILDREN);

            if (ffmpeg.avcodec_open2(decoder._context, codec, null) < 0)
            {
                decoder.Dispose();

                return null;
            }

            return decoder;
        }

        public string Decode(byte* data, int size, long pts)
        {
            if (ffmpeg.av_new_packet(_packet, size) < 0)
            {
                return null;
            }

            Buffer.MemoryCopy(data, _packet->data, size, size);

            _packet->pts = pts;
            _packet->dts = pts;

            AVSubtitle subtitle;
            var got = 0;

            try
            {
                if (ffmpeg.avcodec_decode_subtitle2(_context, &subtitle, &got, _packet) < 0 || got == 0)
                {
                    return null;
                }

                var text = new StringBuilder();

                for (var i = 0; i < subtitle.num_rects; i++)
                {
                    var rect = subtitle.rects[i];

                    if (rect->ass != null)
                    {
                        text.Append(StripAss(Marshal.PtrToStringUTF8((IntPtr)rect->ass)));
                    }
                    else if (rect->text != null)
                    {
                        text.Append(Marshal.PtrToStringUTF8((IntPtr)rect->text));
                    }
                }

                ffmpeg.avsubtitle_free(&subtitle);

                return text.ToString().Trim('\n');
            }
            finally
            {
                ffmpeg.av_packet_unref(_packet);
            }
        }

        public static string StripAss(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return string.Empty;
            }

            var commas = 0;
            var start = 0;

            for (var i = 0; i < line.Length && commas < 8; i++)
            {
                if (line[i] == ',')
                {
                    commas++;
                    start = i + 1;
                }
            }

            var text = new StringBuilder();
            var inTag = false;

            for (var i = commas == 8 ? start : 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inTag)
                {
                    inTag = c != '}';

                    continue;
                }

                if (c == '{')
                {
                    inTag = true;

                    continue;
                }

                if (c == '\\' && i + 1 < line.Length && (line[i + 1] == 'N' || line[i + 1] == 'n'))
                {
                    text.Append('\n');
                    i++;

                    continue;
                }

                if (c == '\\' && i + 1 < line.Length && line[i + 1] == 'h')
                {
                    text.Append(' ');
                    i++;

                    continue;
                }

                text.Append(c);
            }

            return text.ToString();
        }

        public void Dispose()
        {
            if (_packet != null)
            {
                var packet = _packet;

                ffmpeg.av_packet_free(&packet);

                _packet = null;
            }

            if (_context != null)
            {
                var context = _context;

                ffmpeg.avcodec_free_context(&context);

                _context = null;
            }
        }
    }

    public sealed unsafe class Resampler : IDisposable
    {
        private SwrContext* _swr;

        private AVChannelLayout _inputLayout;

        private AVSampleFormat _inputFormat = AVSampleFormat.AV_SAMPLE_FMT_NONE;

        private int _inputRate;

        private int _outputChannels;

        public Resampler(int outputRate)
        {
            OutputRate = outputRate;
        }

        public int OutputRate { get; }

        public string InputDescription { get; private set; }

        public int Convert(AVFrame* frame, int outputChannels, ref float[] output)
        {
            var changed = _swr == null || frame->sample_rate != _inputRate || (AVSampleFormat)frame->format != _inputFormat || outputChannels != _outputChannels;

            fixed (AVChannelLayout* input = &_inputLayout)
            {
                if (!changed && ffmpeg.av_channel_layout_compare(input, &frame->ch_layout) != 0)
                {
                    changed = true;
                }

                if (changed)
                {
                    Configure(frame, outputChannels, input);
                }
            }

            var capacity = ffmpeg.swr_get_out_samples(_swr, frame->nb_samples);

            if (capacity <= 0)
            {
                return 0;
            }

            if (output == null || output.Length < capacity * _outputChannels)
            {
                output = new float[capacity * _outputChannels * 2];
            }

            fixed (float* target = output)
            {
                var planes = (byte*)target;
                var converted = ffmpeg.swr_convert(_swr, &planes, capacity, frame->extended_data, frame->nb_samples);

                return Math.Max(0, converted);
            }
        }

        private void Configure(AVFrame* frame, int outputChannels, AVChannelLayout* input)
        {
            Free();

            ffmpeg.av_channel_layout_uninit(input);
            ffmpeg.av_channel_layout_copy(input, &frame->ch_layout);

            if (input->nb_channels == 0 || input->order == AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC)
            {
                var count = Math.Max(1, input->nb_channels);

                ffmpeg.av_channel_layout_uninit(input);
                ffmpeg.av_channel_layout_default(input, count);
            }

            _inputFormat = (AVSampleFormat)frame->format;
            _inputRate = frame->sample_rate;
            _outputChannels = outputChannels;

            AVChannelLayout layout;

            ffmpeg.av_channel_layout_default(&layout, outputChannels);

            SwrContext* swr = null;

            FFmpegNative.Check(ffmpeg.swr_alloc_set_opts2(&swr, &layout, AVSampleFormat.AV_SAMPLE_FMT_FLT, OutputRate, input, _inputFormat, _inputRate, 0, null), "Setting up the sound converter");
            FFmpegNative.Check(ffmpeg.swr_init(swr), "Starting the sound converter");

            _swr = swr;

            InputDescription = $"{ffmpeg.av_get_sample_fmt_name(_inputFormat)} {input->nb_channels}ch {_inputRate}Hz to float {outputChannels}ch {OutputRate}Hz";

            TvCore.LogInfo($"[Player] Sound converter: {InputDescription}");
        }

        private void Free()
        {
            if (_swr != null)
            {
                var swr = _swr;

                ffmpeg.swr_free(&swr);

                _swr = null;
            }
        }

        public void Dispose()
        {
            Free();

            fixed (AVChannelLayout* input = &_inputLayout)
            {
                ffmpeg.av_channel_layout_uninit(input);
            }
        }
    }
}
