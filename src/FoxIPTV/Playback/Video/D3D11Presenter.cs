// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback.Video
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Platform;
    using Avalonia.Rendering.Composition;
    using Classes;
    using FFmpeg.AutoGen;

    public sealed class D3D11Presenter : IDisposable
    {
        private const int RingSize = 3;

        private readonly ICompositionGpuInterop _interop;

        private readonly CompositionDrawingSurface _surface;

        private readonly Direct3D _d3d;

        private readonly Dictionary<(IntPtr Texture, int Slice), IntPtr> _inputs = new Dictionary<(IntPtr, int), IntPtr>();

        private readonly Target[] _ring = new Target[RingSize];

        private IntPtr _enumerator;

        private IntPtr _processor;

        private (uint Width, uint Height, int Format, bool Interlaced, int OutWidth, int OutHeight) _processorKey;

        private IntPtr _upload;

        private (int Width, int Height) _uploadSize;

        private byte[] _uploadBuffer;

        private unsafe SwsContext* _uploadScaler;

        private PixelSize _ringSize;

        private int _next;

        private bool _loggedFormat;

        private int _failures;

        private bool _loggedHandOff;

        private bool _lost;

        private D3D11Presenter(ICompositionGpuInterop interop, CompositionDrawingSurface surface, Direct3D d3d)
        {
            _interop = interop;
            _surface = surface;
            _d3d = d3d;
        }

        public string Adapter => _d3d.Adapter;

        public IntPtr DevicePointer => _d3d.Device;

        public bool IsLost => _lost || _interop.IsLost || _d3d.IsRemoved;

        public static D3D11Presenter Create(ICompositionGpuInterop interop, CompositionDrawingSurface surface)
        {
            if (!interop.SupportedImageHandleTypes.Contains(KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle))
            {
                TvCore.LogInfo($"[Player] The window can't take D3D11 textures (it offers {string.Join(", ", interop.SupportedImageHandleTypes)})");

                return null;
            }

            var d3d = Direct3D.Create(interop.DeviceLuid);

            TvCore.LogInfo($"[Player] D3D11 video on {d3d.Adapter} (feature level {d3d.FeatureLevel >> 12}.{(d3d.FeatureLevel >> 8) & 0xF})");

            return new D3D11Presenter(interop, surface, d3d);
        }

        public unsafe bool Present(VideoFrame frame, PixelSize size, double forcedAspect, bool fill)
        {
            if (_lost || size.Width <= 0 || size.Height <= 0 || frame?.Frame == null)
            {
                return false;
            }

            var av = frame.Frame;

            IntPtr texture;
            int slice;
            var borrowed = false;

            if (frame.IsHardware && (AVPixelFormat)av->format == AVPixelFormat.AV_PIX_FMT_D3D11)
            {
                texture = (IntPtr)av->data[0];
                slice = (int)(IntPtr)av->data[1];
                borrowed = true;

                Direct3D.AddRef(texture);
            }
            else
            {
                texture = Upload(av);
                slice = 0;

                if (texture == IntPtr.Zero)
                {
                    return false;
                }
            }

            try
            {
                var description = Direct3D.Describe(texture);
                var interlaced = (av->flags & ffmpeg.AV_FRAME_FLAG_INTERLACED) != 0;

                if (!EnsureProcessor(description.Width, description.Height, description.Format, interlaced, size))
                {
                    return false;
                }

                EnsureRing(size);

                var target = NextFree();

                if (target == null)
                {
                    return false;
                }

                var input = Input(texture, slice);

                if (input == IntPtr.Zero)
                {
                    return false;
                }

                if (target.OutputView == IntPtr.Zero)
                {
                    target.OutputView = _d3d.CreateOutputView(target.Texture, _enumerator);
                }

                if (!Direct3D.Acquire(target.Mutex, 0, 100))
                {
                    return false;
                }

                int result;

                try
                {
                    Configure(av, interlaced, size, frame.DisplayAspect(forcedAspect), fill);

                    result = _d3d.Blt(_processor, target.OutputView, input, (uint)(frame.Number & 0xFFFFFFF));

                    _d3d.Flush();
                }
                finally
                {
                    Direct3D.ReleaseKey(target.Mutex, 1);
                }

                if (result < 0)
                {
                    throw new Direct3DException("VideoProcessorBlt", result);
                }

                Submit(target);

                return true;
            }
            catch (Exception ex)
            {
                if (ex is Direct3DException d3dFailure && d3dFailure.IsDeviceLoss || _d3d.IsRemoved)
                {
                    _lost = true;

                    TvCore.LogError($"[Player] The graphics device was lost: {ex.Message}");
                }
                else if (++_failures <= 3 || _failures % 600 == 0)
                {
                    TvCore.LogError($"[Player] D3D11 present failed ({_failures} so far): {ex.GetType().Name}: {ex.Message}");
                }

                return false;
            }
            finally
            {
                if (borrowed)
                {
                    Direct3D.Release(texture);
                }
            }
        }

        public void Clear(PixelSize size)
        {
            ClearInputs();

            if (_lost || size.Width <= 0 || size.Height <= 0)
            {
                return;
            }

            try
            {
                EnsureRing(size);

                var target = NextFree();

                if (target == null || !Direct3D.Acquire(target.Mutex, 0, 100))
                {
                    return;
                }

                try
                {
                    _d3d.Clear(target.RenderView, 0, 0, 0, 1);
                    _d3d.Flush();
                }
                finally
                {
                    Direct3D.ReleaseKey(target.Mutex, 1);
                }

                Submit(target);
            }
            catch (Exception ex)
            {
                _lost |= ex is Direct3DException failure && failure.IsDeviceLoss || _d3d.IsRemoved;

                TvCore.LogError($"[Player] D3D11 clear failed: {ex.Message}");
            }
        }

        private void Submit(Target target)
        {
            target.Imported ??= _interop.ImportImage(new PlatformHandle(target.SharedHandle, KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle), new PlatformGraphicsExternalImageProperties
            {
                Width = target.Size.Width,
                Height = target.Size.Height,
                Format = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm,
                TopLeftOrigin = true
            });

            target.LastPresent = _surface.UpdateWithKeyedMutexAsync(target.Imported, 1, 0);

            _next = (_next + 1) % RingSize;
        }

        private Target NextFree()
        {
            for (var i = 0; i < RingSize; i++)
            {
                var index = (_next + i) % RingSize;
                var target = _ring[index];

                if (target != null && (target.LastPresent == null || target.LastPresent.IsCompleted))
                {
                    if (target.LastPresent != null && target.LastPresent.IsFaulted && !_loggedHandOff)
                    {
                        _loggedHandOff = true;

                        TvCore.LogError($"[Player] The window refused a video texture: {target.LastPresent.Exception?.GetBaseException().Message}");
                    }

                    _next = index;

                    return target;
                }
            }

            return null;
        }

        private bool EnsureProcessor(uint width, uint height, int format, bool interlaced, PixelSize output)
        {
            var key = (width, height, format, interlaced, output.Width, output.Height);

            if (_processor != IntPtr.Zero && key == _processorKey)
            {
                return true;
            }

            FreeProcessor();

            var content = new Direct3D.ContentDesc
            {
                InputFrameFormat = interlaced ? Direct3D.FrameTopFieldFirst : Direct3D.FrameProgressive,
                InputRateNumerator = 30,
                InputRateDenominator = 1,
                InputWidth = width,
                InputHeight = height,
                OutputRateNumerator = 30,
                OutputRateDenominator = 1,
                OutputWidth = (uint)output.Width,
                OutputHeight = (uint)output.Height,
                Usage = 0
            };

            try
            {
                _enumerator = _d3d.CreateProcessorEnumerator(content);

                if ((Direct3D.ProcessorFormatSupport(_enumerator, format) & Direct3D.ProcessorFormatInput) == 0 || (Direct3D.ProcessorFormatSupport(_enumerator, Direct3D.FormatBgra) & Direct3D.ProcessorFormatOutput) == 0)
                {
                    TvCore.LogError($"[Player] The GPU's video processor can't take format {format} in or BGRA out");

                    FreeProcessor();

                    return false;
                }

                _processor = _d3d.CreateProcessor(_enumerator);

                _d3d.SetStreamAutoProcessing(_processor, false);
                _d3d.SetOutputBackground(_processor, 0, 0, 0);
                _d3d.SetOutputColorSpace(_processor, Direct3D.ColorSpaceRgbFullP709);

                _processorKey = key;

                if (!_loggedFormat)
                {
                    _loggedFormat = true;

                    TvCore.LogInfo($"[Player] Video processor: {width}x{height} format {format}{(interlaced ? " interlaced" : string.Empty)} to {output.Width}x{output.Height} BGRA");
                }

                return true;
            }
            catch (Exception ex)
            {
                _lost |= ex is Direct3DException failure && failure.IsDeviceLoss || _d3d.IsRemoved;

                TvCore.LogError($"[Player] Video processor setup failed for {width}x{height} format {format}: {ex.Message}");

                FreeProcessor();

                return false;
            }
        }

        private void FreeProcessor()
        {
            ClearInputs();

            foreach (var target in _ring.Where(x => x != null))
            {
                Direct3D.Release(target.OutputView);

                target.OutputView = IntPtr.Zero;
            }

            Direct3D.Release(_processor);
            Direct3D.Release(_enumerator);

            _processor = IntPtr.Zero;
            _enumerator = IntPtr.Zero;
        }

        private unsafe void Configure(AVFrame* frame, bool interlaced, PixelSize output, double aspect, bool fill)
        {
            var width = frame->width;
            var height = frame->height;
            var source = VideoSurface.FillSource(width, height, aspect, output.Width, output.Height, fill);

            var fitWidth = output.Width;
            var fitHeight = (int)Math.Round(output.Width / aspect);

            if (fill)
            {
                fitHeight = output.Height;
            }
            else if (fitHeight > output.Height)
            {
                fitHeight = output.Height;
                fitWidth = (int)Math.Round(output.Height * aspect);
            }

            var left = (output.Width - fitWidth) / 2;
            var top = (output.Height - fitHeight) / 2;

            var format = interlaced ? ((frame->flags & ffmpeg.AV_FRAME_FLAG_TOP_FIELD_FIRST) != 0 ? Direct3D.FrameTopFieldFirst : Direct3D.FrameBottomFieldFirst) : Direct3D.FrameProgressive;

            _d3d.SetStreamFrameFormat(_processor, format);
            _d3d.SetStreamOutputRate(_processor, interlaced ? Direct3D.OutputRateHalf : Direct3D.OutputRateNormal);
            _d3d.SetStreamSourceRect(_processor, new Direct3D.Rect((int)source.X, (int)source.Y, (int)(source.X + source.Width), (int)(source.Y + source.Height)));
            _d3d.SetStreamDestRect(_processor, new Direct3D.Rect(left, top, left + fitWidth, top + fitHeight));
            _d3d.SetOutputTargetRect(_processor, new Direct3D.Rect(0, 0, output.Width, output.Height));
            _d3d.SetStreamColorSpace(_processor, ColorSpace(frame));
        }

        private static unsafe int ColorSpace(AVFrame* frame)
        {
            var full = frame->color_range == AVColorRange.AVCOL_RANGE_JPEG;
            var matrix = frame->colorspace;

            if (matrix == AVColorSpace.AVCOL_SPC_UNSPECIFIED)
            {
                matrix = frame->height >= 720 ? AVColorSpace.AVCOL_SPC_BT709 : AVColorSpace.AVCOL_SPC_SMPTE170M;
            }

            if (matrix == AVColorSpace.AVCOL_SPC_BT2020_NCL || matrix == AVColorSpace.AVCOL_SPC_BT2020_CL)
            {
                switch (frame->color_trc)
                {
                    case AVColorTransferCharacteristic.AVCOL_TRC_SMPTE2084:
                    {
                        return Direct3D.ColorSpaceStudioPqP2020;
                    }

                    case AVColorTransferCharacteristic.AVCOL_TRC_ARIB_STD_B67:
                    {
                        return full ? Direct3D.ColorSpaceFullHlgP2020 : Direct3D.ColorSpaceStudioHlgP2020;
                    }

                    default:
                    {
                        return full ? Direct3D.ColorSpaceFullP2020 : Direct3D.ColorSpaceStudioP2020;
                    }
                }
            }

            if (matrix == AVColorSpace.AVCOL_SPC_BT709)
            {
                return full ? Direct3D.ColorSpaceFullP709 : Direct3D.ColorSpaceStudioP709;
            }

            return full ? Direct3D.ColorSpaceFullP601 : Direct3D.ColorSpaceStudioP601;
        }

        private IntPtr Input(IntPtr texture, int slice)
        {
            var key = (texture, slice);

            if (_inputs.TryGetValue(key, out var view))
            {
                return view;
            }

            if (_inputs.Count >= 96)
            {
                ClearInputs();
            }

            try
            {
                view = _d3d.CreateInputView(texture, _enumerator, (uint)slice);

                _inputs[key] = view;

                return view;
            }
            catch (Exception ex)
            {
                _lost |= ex is Direct3DException failure && failure.IsDeviceLoss;

                TvCore.LogError($"[Player] Video processor input view failed: {ex.Message}");

                return IntPtr.Zero;
            }
        }

        private void ClearInputs()
        {
            foreach (var view in _inputs.Values)
            {
                Direct3D.Release(view);
            }

            _inputs.Clear();
        }

        private unsafe IntPtr Upload(AVFrame* frame)
        {
            var width = frame->width & ~1;
            var height = frame->height & ~1;

            if (width <= 0 || height <= 0)
            {
                return IntPtr.Zero;
            }

            if (_upload == IntPtr.Zero || _uploadSize != (width, height))
            {
                ClearInputs();

                Direct3D.Release(_upload);

                _upload = _d3d.CreateTexture((uint)width, (uint)height, Direct3D.FormatNv12, Direct3D.UsageDefault, Direct3D.BindDecoder, 0, 0);
                _uploadSize = (width, height);
                _uploadBuffer = new byte[width * height * 3 / 2];
            }

            _uploadScaler = ffmpeg.sws_getCachedContext(_uploadScaler, frame->width, frame->height, (AVPixelFormat)frame->format, width, height, AVPixelFormat.AV_PIX_FMT_NV12, (int)SwsFlags.SWS_BILINEAR, null, null, null);

            if (_uploadScaler == null)
            {
                return IntPtr.Zero;
            }

            fixed (byte* buffer = _uploadBuffer)
            {
                var planes = new byte*[] { buffer, buffer + width * height, null, null };
                var strides = new[] { width, width, 0, 0 };
                var sources = new byte*[] { frame->data[0], frame->data[1], frame->data[2], frame->data[3] };
                var sourceStrides = new[] { frame->linesize[0], frame->linesize[1], frame->linesize[2], frame->linesize[3] };

                ffmpeg.sws_scale(_uploadScaler, sources, sourceStrides, 0, frame->height, planes, strides);

                _d3d.UpdateSubresource(_upload, (IntPtr)buffer, (uint)width, (uint)(width * height * 3 / 2));
            }

            return _upload;
        }

        private void EnsureRing(PixelSize size)
        {
            if (size == _ringSize && _ring.All(x => x != null))
            {
                return;
            }

            var old = _ring.ToArray();

            for (var i = 0; i < RingSize; i++)
            {
                _ring[i] = new Target(_d3d, size);
            }

            _ringSize = size;
            _next = 0;

            _ = Task.Run(async () =>
            {
                foreach (var target in old)
                {
                    if (target != null)
                    {
                        await target.DisposeAsync().ConfigureAwait(false);
                    }
                }
            });
        }

        public unsafe void Dispose()
        {
            ClearInputs();

            foreach (var target in _ring)
            {
                target?.DisposeAsync().AsTask().Wait(500);
            }

            if (_uploadScaler != null)
            {
                ffmpeg.sws_freeContext(_uploadScaler);

                _uploadScaler = null;
            }

            Direct3D.Release(_upload);

            _upload = IntPtr.Zero;

            FreeProcessor();

            _d3d.Dispose();
        }

        private sealed class Target
        {
            public Target(Direct3D d3d, PixelSize size)
            {
                Size = size;
                Texture = d3d.CreateTexture((uint)size.Width, (uint)size.Height, Direct3D.FormatBgra, Direct3D.UsageDefault, Direct3D.BindRenderTarget | Direct3D.BindShaderResource, 0, Direct3D.MiscSharedKeyedMutex);
                Mutex = Direct3D.KeyedMutex(Texture);
                SharedHandle = Direct3D.SharedHandle(Texture);
                RenderView = d3d.CreateRenderTargetView(Texture);
            }

            public PixelSize Size { get; }

            public IntPtr Texture { get; }

            public IntPtr Mutex { get; }

            public IntPtr SharedHandle { get; }

            public IntPtr RenderView { get; }

            public IntPtr OutputView { get; set; }

            public ICompositionImportedGpuImage Imported { get; set; }

            public Task LastPresent { get; set; }

            public async ValueTask DisposeAsync()
            {
                if (LastPresent != null)
                {
                    try
                    {
                        await LastPresent.ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }
                }

                if (Imported != null)
                {
                    try
                    {
                        await Imported.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }
                }

                Direct3D.Release(OutputView);
                Direct3D.Release(RenderView);
                Direct3D.Release(Mutex);
                Direct3D.Release(Texture);
            }
        }
    }
}
