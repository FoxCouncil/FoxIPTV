// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback.Video
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;
    using Avalonia.Media.Imaging;
    using Avalonia.Platform;
    using Avalonia.Rendering.Composition;
    using Avalonia.VisualTree;
    using Classes;
    using FFmpeg.AutoGen;

    public sealed class VideoSurface : Control
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private readonly Action _update;

        private Player _player;

        private Compositor _compositor;

        private CompositionSurfaceVisual _visual;

        private CompositionDrawingSurface _surface;

        private D3D11Presenter _d3d;

        private bool _initialized;

        private bool _recovering;

        private bool _updateQueued;

        private double _queuedAt;

        private bool _dirty;

        private VideoFrame _current;

        private VideoFrame _pending;

        private double _lastTick = double.NaN;

        private double _interval = 1 / 60.0;

        private string _aspectRatio;

        private WriteableBitmap _bitmap;

        private unsafe SwsContext* _scaler;

        private Rect _bitmapRect;

        public VideoSurface()
        {
            _update = OnCompositionUpdate;

            ClipToBounds = true;
        }

        public Player Player
        {
            get => _player;
            set
            {
                _player = value;

                if (_player != null && _d3d != null && _player.Hardware == null && FFmpegNative.Initialize())
                {
                    _player.Hardware = HardwareDevice.FromD3D11(_d3d.DevicePointer);
                    _player.WantsCpuFrames = false;
                }

                Queue();
            }
        }

        public string AspectRatio
        {
            get => _aspectRatio;
            set
            {
                _aspectRatio = value;
                _dirty = true;

                Queue();
                InvalidateVisual();
            }
        }

        public string Renderer { get; private set; } = "starting";

        public event Action<int, int> PictureShown;

        private bool _announced;

        public void Clear()
        {
            _announced = false;

            _pending?.Free();
            _pending = null;

            _current?.Free();
            _current = null;

            if (_d3d != null)
            {
                _d3d.Clear(PixelSizeNow());
            }
            else
            {
                _bitmapRect = default;

                InvalidateVisual();
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _ = InitializeAsync();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty)
            {
                _dirty = true;

                if (_visual != null)
                {
                    _visual.Size = new Vector(Bounds.Width, Bounds.Height);
                }

                Queue();
            }
        }

        private async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                _compositor = ElementComposition.GetElementVisual(this)?.Compositor;

                if (_compositor != null && OperatingSystem.IsWindows())
                {
                    var interop = await _compositor.TryGetCompositionGpuInterop();

                    if (interop != null)
                    {
                        _surface = _compositor.CreateDrawingSurface();
                        _visual = _compositor.CreateSurfaceVisual();
                        _visual.Size = new Vector(Bounds.Width, Bounds.Height);
                        _visual.Surface = _surface;

                        _d3d = D3D11Presenter.Create(interop, _surface);

                        if (_d3d != null)
                        {
                            ElementComposition.SetElementChildVisual(this, _visual);

                            if (_player != null && FFmpegNative.Initialize())
                            {
                                _player.Hardware = HardwareDevice.FromD3D11(_d3d.DevicePointer);
                                _player.WantsCpuFrames = false;
                            }

                            Renderer = $"GPU (D3D11 on {_d3d.Adapter})";
                        }
                        else
                        {
                            _surface.Dispose();
                            _surface = null;
                            _visual = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Player] GPU video setup failed, drawing from memory instead: {ex.GetType().Name}: {ex.Message}");

                _d3d = null;
            }

            if (_d3d == null)
            {
                Renderer = "bitmap";

                if (_player != null)
                {
                    _player.WantsCpuFrames = true;
                }
            }

            TvCore.LogInfo($"[Player] Video drawn by {Renderer}");

            Queue();
        }

        private async Task RecoverAsync()
        {
            _recovering = true;

            TvCore.LogError("[Player] The graphics device or the window's side of the picture hand-off was lost, rebuilding the picture path");

            var old = _d3d;

            _d3d = null;

            _pending?.Free();
            _pending = null;

            _current?.Free();
            _current = null;

            ElementComposition.SetElementChildVisual(this, null);

            _surface?.Dispose();
            _surface = null;
            _visual = null;

            try
            {
                old.Dispose();
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Player] Releasing the lost graphics device: {ex.Message}");
            }

            if (_player != null)
            {
                _player.Hardware = null;
            }

            _initialized = false;

            await InitializeAsync();

            _recovering = false;

            _player?.Restart();

            Queue();
        }

        private void Queue()
        {
            if (!_initialized || _updateQueued)
            {
                return;
            }

            _compositor ??= ElementComposition.GetElementVisual(this)?.Compositor;

            if (_compositor == null)
            {
                return;
            }

            _updateQueued = true;
            _queuedAt = _clock.Elapsed.TotalSeconds;

            _compositor.RequestCompositionUpdate(_update);
        }

        private void OnCompositionUpdate()
        {
            _updateQueued = false;

            if (this.GetVisualRoot() == null || _recovering)
            {
                return;
            }

            if (_d3d != null && _d3d.IsLost)
            {
                _ = RecoverAsync();

                return;
            }

            var now = _clock.Elapsed.TotalSeconds;

            if (!double.IsNaN(_lastTick))
            {
                var gap = now - _lastTick;

                if (gap > 0.004 && gap < 0.1)
                {
                    _interval = _interval * 0.9 + gap * 0.1;
                }
            }

            _lastTick = now;

            var frame = _pending ?? _player?.TakeFrame(_interval);

            _pending = null;

            if (frame != null)
            {
                if (Show(frame))
                {
                    if (!ReferenceEquals(_current, frame))
                    {
                        _current?.Free();
                    }

                    _current = frame;
                    _dirty = false;

                    if (!_announced)
                    {
                        _announced = true;

                        PictureShown?.Invoke(frame.Width, frame.Height);
                    }
                }
                else
                {
                    _pending = frame;
                }
            }
            else if (_dirty && _current != null)
            {
                _dirty = !Show(_current);
            }

            if (_player != null && _player.IsActive || _pending != null || _dirty)
            {
                Queue();
            }
            else
            {
                _lastTick = double.NaN;
            }
        }

        public void Wake()
        {
            if (_updateQueued && _clock.Elapsed.TotalSeconds - _queuedAt > 1)
            {
                _updateQueued = false;
            }

            Queue();
        }

        private PixelSize PixelSizeNow()
        {
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

            return PixelSize.FromSize(Bounds.Size, scale);
        }

        private double ForcedAspect()
        {
            var ratio = _aspectRatio;

            if (string.IsNullOrWhiteSpace(ratio))
            {
                return 0;
            }

            var parts = ratio.Split(':');

            if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width) && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height) && width > 0 && height > 0)
            {
                return width / height;
            }

            return 0;
        }

        private bool Show(VideoFrame frame)
        {
            if (_d3d != null)
            {
                return _d3d.Present(frame, PixelSizeNow(), ForcedAspect());
            }

            return ShowBitmap(frame);
        }

        private unsafe bool ShowBitmap(VideoFrame frame)
        {
            var av = frame.Frame;

            if (av == null || frame.IsHardware || av->width <= 0 || av->height <= 0)
            {
                return true;
            }

            var width = av->width;
            var height = av->height;

            if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            }

            _scaler = ffmpeg.sws_getCachedContext(_scaler, width, height, (AVPixelFormat)av->format, width, height, AVPixelFormat.AV_PIX_FMT_BGRA, (int)SwsFlags.SWS_BILINEAR, null, null, null);

            if (_scaler == null)
            {
                return true;
            }

            using (var buffer = _bitmap.Lock())
            {
                var target = new byte*[] { (byte*)buffer.Address, null, null, null };
                var strides = new[] { buffer.RowBytes, 0, 0, 0 };
                var sources = new byte*[] { av->data[0], av->data[1], av->data[2], av->data[3] };
                var sourceStrides = new[] { av->linesize[0], av->linesize[1], av->linesize[2], av->linesize[3] };

                ffmpeg.sws_scale(_scaler, sources, sourceStrides, 0, height, target, strides);
            }

            var sar = av->sample_aspect_ratio;
            var pixelAspect = sar.num > 0 && sar.den > 0 ? sar.num / (double)sar.den : 1.0;
            var forced = ForcedAspect();

            _bitmapAspect = forced > 0 ? forced : width * pixelAspect / height;
            _bitmapRect = new Rect(0, 0, width, height);

            InvalidateVisual();

            return true;
        }

        private double _bitmapAspect = 16 / 9.0;

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

            if (_d3d != null || _bitmap == null || _bitmapRect.Width <= 0)
            {
                return;
            }

            var bounds = new Rect(Bounds.Size);
            var width = bounds.Width;
            var height = width / _bitmapAspect;

            if (height > bounds.Height)
            {
                height = bounds.Height;
                width = height * _bitmapAspect;
            }

            var target = new Rect((bounds.Width - width) / 2, (bounds.Height - height) / 2, width, height);

            context.DrawImage(_bitmap, _bitmapRect, target);
        }
    }
}
