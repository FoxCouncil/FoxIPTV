// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback.Video
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Platform;
    using Avalonia.Rendering.Composition;
    using Classes;
    using FFmpeg.AutoGen;
    using Vortice;
    using Vortice.Direct3D;
    using Vortice.Direct3D11;
    using Vortice.DXGI;
    using Vortice.Mathematics;
    using ID3D11Device = Vortice.Direct3D11.ID3D11Device;
    using ID3D11DeviceContext = Vortice.Direct3D11.ID3D11DeviceContext;
    using ID3D11Texture2D = Vortice.Direct3D11.ID3D11Texture2D;
    using ID3D11VideoContext = Vortice.Direct3D11.ID3D11VideoContext;
    using ID3D11VideoDevice = Vortice.Direct3D11.ID3D11VideoDevice;

    /// <summary>
    /// Draws decoded frames on Windows without them leaving the GPU: the D3D11 video processor
    /// converts, scales and deinterlaces into a shared texture that Avalonia composites directly.
    /// </summary>
    public sealed class D3D11Presenter : IDisposable
    {
        private const int RingSize = 3;

        private const int WaitTimeout = 0x102;

        private readonly ICompositionGpuInterop _interop;

        private readonly CompositionDrawingSurface _surface;

        private readonly ID3D11Device _device;

        private readonly ID3D11DeviceContext _context;

        private readonly ID3D11VideoDevice _videoDevice;

        private readonly ID3D11VideoContext _videoContext;

        private readonly ID3D11VideoContext1 _videoContext1;

        private readonly Dictionary<(IntPtr Texture, int Slice), ID3D11VideoProcessorInputView> _inputs = new Dictionary<(IntPtr, int), ID3D11VideoProcessorInputView>();

        private readonly Target[] _ring = new Target[RingSize];

        private ID3D11VideoProcessorEnumerator _enumerator;

        private ID3D11VideoProcessor _processor;

        private (uint Width, uint Height, Format Format, bool Interlaced, int OutWidth, int OutHeight) _processorKey;

        private ID3D11Texture2D _upload;

        private (int Width, int Height) _uploadSize;

        private byte[] _uploadBuffer;

        private unsafe SwsContext* _uploadScaler;

        private PixelSize _ringSize;

        private int _next;

        private bool _loggedFormat;

        private int _failures;

        private bool _loggedHandOff;

        private D3D11Presenter(ICompositionGpuInterop interop, CompositionDrawingSurface surface, ID3D11Device device, ID3D11DeviceContext context, string adapter)
        {
            _interop = interop;
            _surface = surface;
            _device = device;
            _context = context;
            _videoDevice = device.QueryInterface<ID3D11VideoDevice>();
            _videoContext = context.QueryInterface<ID3D11VideoContext>();
            _videoContext1 = context.QueryInterfaceOrNull<ID3D11VideoContext1>();

            Adapter = adapter;
        }

        public string Adapter { get; }

        public IntPtr DevicePointer => _device.NativePointer;

        public static D3D11Presenter Create(ICompositionGpuInterop interop, CompositionDrawingSurface surface)
        {
            if (!interop.SupportedImageHandleTypes.Contains(KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle))
            {
                TvCore.LogInfo($"[Player] The window can't take D3D11 textures (it offers {string.Join(", ", interop.SupportedImageHandleTypes)})");

                return null;
            }

            using (var factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>())
            {
                IDXGIAdapter1 adapter = null;

                var luid = interop.DeviceLuid;

                if (luid != null && luid.Length == 8)
                {
                    try
                    {
                        adapter = factory.EnumAdapterByLuid<IDXGIAdapter1>(new Luid(BitConverter.ToUInt32(luid, 0), BitConverter.ToInt32(luid, 4)));
                    }
                    catch (Exception ex)
                    {
                        TvCore.LogError($"[Player] Adapter for the window not found ({ex.Message}), using the first one");
                    }
                }

                if (adapter == null)
                {
                    factory.EnumAdapters1(0, out adapter).CheckError();
                }

                using (adapter)
                {
                    var levels = new[] { FeatureLevel.Level_12_1, FeatureLevel.Level_12_0, FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 };

                    D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.VideoSupport | DeviceCreationFlags.BgraSupport, levels, out ID3D11Device device, out ID3D11DeviceContext context).CheckError();

                    using (var multithread = context.QueryInterface<ID3D11Multithread>())
                    {
                        multithread.SetMultithreadProtected(true);
                    }

                    var name = adapter.Description1.Description;

                    TvCore.LogInfo($"[Player] D3D11 video on {name} ({device.FeatureLevel})");

                    return new D3D11Presenter(interop, surface, device, context, name);
                }
            }
        }

        /// <summary>Draws a frame letterboxed into a texture the size of the control and hands it to the compositor</summary>
        public unsafe bool Present(VideoFrame frame, PixelSize size, double forcedAspect)
        {
            if (size.Width <= 0 || size.Height <= 0 || frame?.Frame == null)
            {
                return false;
            }

            var av = frame.Frame;

            ID3D11Texture2D texture;
            int slice;

            if (frame.IsHardware && (AVPixelFormat)av->format == AVPixelFormat.AV_PIX_FMT_D3D11)
            {
                var pointer = (IntPtr)av->data[0];

                Marshal.AddRef(pointer);

                texture = new ID3D11Texture2D(pointer);
                slice = (int)(IntPtr)av->data[1];
            }
            else
            {
                texture = Upload(av);
                slice = 0;

                if (texture == null)
                {
                    return false;
                }
            }

            try
            {
                var description = texture.Description;
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

                if (input == null)
                {
                    return false;
                }

                target.OutputView ??= _videoDevice.CreateVideoProcessorOutputView(target.Texture, _enumerator, new VideoProcessorOutputViewDescription { ViewDimension = VideoProcessorOutputViewDimension.Texture2D });

                if (!Acquire(target.Mutex, 0, 100))
                {
                    return false;
                }

                try
                {
                    Configure(av, interlaced, size, forcedAspect);

                    var stream = new VideoProcessorStream { Enable = true, InputSurface = input, OutputIndex = 0, InputFrameOrField = (uint)(frame.Number & 0xFFFFFFF) };

                    _videoContext.VideoProcessorBlt(_processor, target.OutputView, 0, 1, new[] { stream });
                    _context.Flush();
                }
                finally
                {
                    target.Mutex.ReleaseSync(1);
                }

                Submit(target);

                return true;
            }
            catch (Exception ex)
            {
                if (++_failures <= 3 || _failures % 600 == 0)
                {
                    TvCore.LogError($"[Player] D3D11 present failed ({_failures} so far): {ex.GetType().Name}: {ex.Message}");
                }

                return false;
            }
            finally
            {
                if (!ReferenceEquals(texture, _upload))
                {
                    texture.Dispose();
                }
            }
        }

        /// <summary>Fills the screen with black</summary>
        public void Clear(PixelSize size)
        {
            if (size.Width <= 0 || size.Height <= 0)
            {
                return;
            }

            EnsureRing(size);

            var target = NextFree();

            if (target == null || !Acquire(target.Mutex, 0, 100))
            {
                return;
            }

            try
            {
                _context.ClearRenderTargetView(target.RenderView, new Color4(0, 0, 0, 1));
                _context.Flush();
            }
            finally
            {
                target.Mutex.ReleaseSync(1);
            }

            Submit(target);
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

        private static unsafe bool Acquire(IDXGIKeyedMutex mutex, ulong key, int milliseconds)
        {
            var vtable = *(IntPtr**)mutex.NativePointer;
            var acquire = (delegate* unmanaged[Stdcall]<IntPtr, ulong, uint, int>)vtable[8];
            var result = acquire(mutex.NativePointer, key, (uint)milliseconds);

            return result == 0;
        }

        private bool EnsureProcessor(uint width, uint height, Format format, bool interlaced, PixelSize output)
        {
            var key = (width, height, format, interlaced, output.Width, output.Height);

            if (_processor != null && key == _processorKey)
            {
                return true;
            }

            _processor?.Dispose();
            _enumerator?.Dispose();

            _processor = null;
            _enumerator = null;

            ClearInputs();

            foreach (var target in _ring.Where(x => x != null))
            {
                target.OutputView?.Dispose();
                target.OutputView = null;
            }

            var content = new VideoProcessorContentDescription
            {
                InputFrameFormat = interlaced ? VideoFrameFormat.InterlacedTopFieldFirst : VideoFrameFormat.Progressive,
                InputFrameRate = new Rational(30, 1),
                InputWidth = width,
                InputHeight = height,
                OutputFrameRate = new Rational(30, 1),
                OutputWidth = (uint)output.Width,
                OutputHeight = (uint)output.Height,
                Usage = VideoUsage.PlaybackNormal
            };

            try
            {
                _enumerator = _videoDevice.CreateVideoProcessorEnumerator(content);

                var inputSupport = _enumerator.CheckVideoProcessorFormat(format);
                var outputSupport = _enumerator.CheckVideoProcessorFormat(Format.B8G8R8A8_UNorm);

                if ((inputSupport & VideoProcessorFormatSupport.Input) == 0 || (outputSupport & VideoProcessorFormatSupport.Output) == 0)
                {
                    TvCore.LogError($"[Player] The GPU's video processor can't take {format} in or BGRA out");

                    return false;
                }

                _processor = _videoDevice.CreateVideoProcessor(_enumerator, 0);

                _videoContext.VideoProcessorSetStreamAutoProcessingMode(_processor, 0, false);
                _videoContext.VideoProcessorSetOutputBackgroundColor(_processor, false, new VideoColor { Rgba = new VideoColorRgba { R = 0, G = 0, B = 0, A = 1 } });
                _videoContext1?.VideoProcessorSetOutputColorSpace1(_processor, ColorSpaceType.RgbFullG22NoneP709);

                _processorKey = key;

                if (!_loggedFormat)
                {
                    _loggedFormat = true;

                    TvCore.LogInfo($"[Player] Video processor: {width}x{height} {format}{(interlaced ? " interlaced" : string.Empty)} to {output.Width}x{output.Height} BGRA");
                }

                return true;
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Player] Video processor setup failed for {width}x{height} {format}: {ex.Message}");

                _processor?.Dispose();
                _enumerator?.Dispose();
                _processor = null;
                _enumerator = null;

                return false;
            }
        }

        private unsafe void Configure(AVFrame* frame, bool interlaced, PixelSize output, double forcedAspect)
        {
            var width = frame->width;
            var height = frame->height;

            var sar = frame->sample_aspect_ratio;
            var pixelAspect = sar.num > 0 && sar.den > 0 ? sar.num / (double)sar.den : 1.0;
            var aspect = forcedAspect > 0 ? forcedAspect : width * pixelAspect / Math.Max(1, height);

            var outWidth = output.Width;
            var outHeight = output.Height;

            var fitWidth = outWidth;
            var fitHeight = (int)Math.Round(outWidth / aspect);

            if (fitHeight > outHeight)
            {
                fitHeight = outHeight;
                fitWidth = (int)Math.Round(outHeight * aspect);
            }

            var left = (outWidth - fitWidth) / 2;
            var top = (outHeight - fitHeight) / 2;

            var format = interlaced ? ((frame->flags & ffmpeg.AV_FRAME_FLAG_TOP_FIELD_FIRST) != 0 ? VideoFrameFormat.InterlacedTopFieldFirst : VideoFrameFormat.InterlacedBottomFieldFirst) : VideoFrameFormat.Progressive;

            _videoContext.VideoProcessorSetStreamFrameFormat(_processor, 0, format);
            _videoContext.VideoProcessorSetStreamOutputRate(_processor, 0, interlaced ? VideoProcessorOutputRate.Half : VideoProcessorOutputRate.Normal, false, null);
            _videoContext.VideoProcessorSetStreamSourceRect(_processor, 0, true, new RawRect(0, 0, width, height));
            _videoContext.VideoProcessorSetStreamDestRect(_processor, 0, true, new RawRect(left, top, left + fitWidth, top + fitHeight));
            _videoContext.VideoProcessorSetOutputTargetRect(_processor, true, new RawRect(0, 0, outWidth, outHeight));

            _videoContext1?.VideoProcessorSetStreamColorSpace1(_processor, 0, ColorSpace(frame));
        }

        private static unsafe ColorSpaceType ColorSpace(AVFrame* frame)
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
                        return ColorSpaceType.YcbcrStudioG2084LeftP2020;
                    }

                    case AVColorTransferCharacteristic.AVCOL_TRC_ARIB_STD_B67:
                    {
                        return full ? ColorSpaceType.YcbcrFullGhlgTopLeftP2020 : ColorSpaceType.YcbcrStudioGhlgTopLeftP2020;
                    }

                    default:
                    {
                        return full ? ColorSpaceType.YcbcrFullG22LeftP2020 : ColorSpaceType.YcbcrStudioG22LeftP2020;
                    }
                }
            }

            if (matrix == AVColorSpace.AVCOL_SPC_BT709)
            {
                return full ? ColorSpaceType.YcbcrFullG22LeftP709 : ColorSpaceType.YcbcrStudioG22LeftP709;
            }

            return full ? ColorSpaceType.YcbcrFullG22LeftP601 : ColorSpaceType.YcbcrStudioG22LeftP601;
        }

        private ID3D11VideoProcessorInputView Input(ID3D11Texture2D texture, int slice)
        {
            var key = (texture.NativePointer, slice);

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
                view = _videoDevice.CreateVideoProcessorInputView(texture, _enumerator, new VideoProcessorInputViewDescription
                {
                    FourCC = 0,
                    ViewDimension = VideoProcessorInputViewDimension.Texture2D,
                    Texture2D = new Texture2DVideoProcessorInputView { MipSlice = 0, ArraySlice = (uint)slice }
                });

                _inputs[key] = view;

                return view;
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Player] Video processor input view failed: {ex.Message}");

                return null;
            }
        }

        private void ClearInputs()
        {
            foreach (var view in _inputs.Values)
            {
                view.Dispose();
            }

            _inputs.Clear();
        }

        /// <summary>Copies a frame decoded in memory into an NV12 texture the video processor can read</summary>
        private unsafe ID3D11Texture2D Upload(AVFrame* frame)
        {
            var width = frame->width & ~1;
            var height = frame->height & ~1;

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            if (_upload == null || _uploadSize != (width, height))
            {
                _upload?.Dispose();

                ClearInputs();

                _upload = _device.CreateTexture2D(new Texture2DDescription
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.NV12,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.Decoder
                });

                _uploadSize = (width, height);
                _uploadBuffer = new byte[width * height * 3 / 2];
            }

            _uploadScaler = ffmpeg.sws_getCachedContext(_uploadScaler, frame->width, frame->height, (AVPixelFormat)frame->format, width, height, AVPixelFormat.AV_PIX_FMT_NV12, (int)SwsFlags.SWS_BILINEAR, null, null, null);

            if (_uploadScaler == null)
            {
                return null;
            }

            fixed (byte* buffer = _uploadBuffer)
            {
                var planes = new byte*[] { buffer, buffer + width * height, null, null };
                var strides = new[] { width, width, 0, 0 };
                var sources = new byte*[] { frame->data[0], frame->data[1], frame->data[2], frame->data[3] };
                var sourceStrides = new[] { frame->linesize[0], frame->linesize[1], frame->linesize[2], frame->linesize[3] };

                ffmpeg.sws_scale(_uploadScaler, sources, sourceStrides, 0, frame->height, planes, strides);

                _context.UpdateSubresource(_upload, 0, null, (IntPtr)buffer, (uint)width, (uint)(width * height * 3 / 2));
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
                _ring[i] = new Target(_device, size);
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

            _upload?.Dispose();
            _processor?.Dispose();
            _enumerator?.Dispose();
            _videoContext1?.Dispose();
            _videoContext?.Dispose();
            _videoDevice?.Dispose();
            _context?.Dispose();
            _device?.Dispose();
        }

        private sealed class Target
        {
            public Target(ID3D11Device device, PixelSize size)
            {
                Size = size;

                Texture = device.CreateTexture2D(new Texture2DDescription
                {
                    Width = (uint)size.Width,
                    Height = (uint)size.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                    MiscFlags = ResourceOptionFlags.SharedKeyedMutex
                });

                Mutex = Texture.QueryInterface<IDXGIKeyedMutex>();

                using (var resource = Texture.QueryInterface<IDXGIResource>())
                {
                    SharedHandle = resource.SharedHandle;
                }

                RenderView = device.CreateRenderTargetView(Texture);
            }

            public PixelSize Size { get; }

            public ID3D11Texture2D Texture { get; }

            public IDXGIKeyedMutex Mutex { get; }

            public IntPtr SharedHandle { get; }

            public ID3D11RenderTargetView RenderView { get; }

            public ID3D11VideoProcessorOutputView OutputView { get; set; }

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
                    await Imported.DisposeAsync().ConfigureAwait(false);
                }

                OutputView?.Dispose();
                RenderView.Dispose();
                Mutex.Dispose();
                Texture.Dispose();
            }
        }
    }
}
