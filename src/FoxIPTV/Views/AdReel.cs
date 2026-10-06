// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Avalonia.Controls;
    using Avalonia.Media;
    using Avalonia.Media.Imaging;
    using Avalonia.Threading;
    using Classes;
    using Playback;
    using Playback.Video;

    public sealed class AdReel : IDisposable
    {
        public const double PictureSeconds = 8;

        private const int DecodeWidth = 1280;

        private const double Gap = 4;

        private readonly Canvas _mosaic;

        private readonly VideoSurface _surface;

        private readonly Queue<string> _pictures = new Queue<string>();

        private readonly Queue<string> _videos = new Queue<string>();

        private readonly List<Bitmap> _shown = new List<Bitmap>();

        private readonly Random _random = new Random();

        private Player _player;

        private string _folder;

        private DateTime _pictureUntil;

        private bool _playingVideo;

        private bool _muted;

        private int _pictureTurns;

        public AdReel(Canvas mosaic, VideoSurface surface)
        {
            _mosaic = mosaic;
            _surface = surface;

            _mosaic.SizeChanged += (sender, args) => LayOut();
        }

        public event Action ItemShown;

        public bool IsRunning { get; private set; }

        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;

                if (_player != null)
                {
                    _player.Muted = value;
                }
            }
        }

        public bool Start(string folder)
        {
            _folder = folder;
            _pictures.Clear();
            _videos.Clear();
            _pictureTurns = 0;

            if (!Refill())
            {
                return false;
            }

            IsRunning = true;

            Next();

            return IsRunning;
        }

        public void Stop()
        {
            IsRunning = false;
            _playingVideo = false;
            _pictures.Clear();
            _videos.Clear();

            _player?.Stop();

            _surface.IsVisible = false;
            _mosaic.IsVisible = false;

            ClearPictures();
        }

        public void Tick()
        {
            if (!IsRunning)
            {
                return;
            }

            if (_playingVideo)
            {
                _surface.Wake();

                return;
            }

            if (DateTime.UtcNow >= _pictureUntil)
            {
                Next();
            }
        }

        public void Dispose()
        {
            Stop();

            _player?.Dispose();
            _player = null;
        }

        private bool Refill()
        {
            var files = MediaFolder.Files(_folder).OrderBy(x => _random.Next()).ToList();

            if (_pictures.Count == 0)
            {
                foreach (var file in files.Where(MediaFolder.IsPicture))
                {
                    _pictures.Enqueue(file);
                }
            }

            if (_videos.Count == 0)
            {
                foreach (var file in files.Where(MediaFolder.IsVideo))
                {
                    _videos.Enqueue(file);
                }
            }

            return _pictures.Count + _videos.Count > 0;
        }

        private void Next()
        {
            for (var tries = 0; IsRunning && tries < 8; tries++)
            {
                if (!Refill())
                {
                    Stop();

                    return;
                }

                var videoTurn = _videos.Count > 0 && (_pictures.Count == 0 || _pictureTurns >= 2);
                var shown = videoTurn ? PlayVideo(_videos.Dequeue()) : ShowPictures();

                if (shown)
                {
                    _pictureTurns = videoTurn ? 0 : _pictureTurns + 1;

                    ItemShown?.Invoke();

                    return;
                }
            }

            Stop();
        }

        private bool ShowPictures()
        {
            var count = Math.Min(_pictures.Count, _random.Next(3, 7));
            var bitmaps = new List<Bitmap>();

            for (var i = 0; i < count && _pictures.Count > 0; i++)
            {
                var path = _pictures.Dequeue();

                try
                {
                    using (var stream = File.OpenRead(path))
                    {
                        bitmaps.Add(Bitmap.DecodeToWidth(stream, DecodeWidth));
                    }
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[Ads] Could not show {Path.GetFileName(path)}: {ex.Message}");
                }
            }

            if (bitmaps.Count == 0)
            {
                return false;
            }

            _player?.Stop();
            _playingVideo = false;
            _surface.IsVisible = false;

            ClearPictures();

            foreach (var bitmap in bitmaps)
            {
                _shown.Add(bitmap);
                _mosaic.Children.Add(new Image { Source = bitmap, Stretch = Stretch.UniformToFill, ClipToBounds = true });
            }

            _mosaic.IsVisible = true;

            LayOut();

            _pictureUntil = DateTime.UtcNow.AddSeconds(PictureSeconds);

            return true;
        }

        private void LayOut()
        {
            var width = _mosaic.Bounds.Width;
            var height = _mosaic.Bounds.Height;

            if (_shown.Count == 0 || width <= 0 || height <= 0)
            {
                return;
            }

            var cells = MosaicLayout.Arrange(_shown.Select(x => x.Size.Height > 0 ? x.Size.Width / x.Size.Height : 1).ToList(), width, height);

            for (var i = 0; i < cells.Count && i < _mosaic.Children.Count; i++)
            {
                var cell = cells[i].Deflate(Gap / 2);
                var image = _mosaic.Children[i];

                Canvas.SetLeft(image, cell.X);
                Canvas.SetTop(image, cell.Y);

                image.Width = Math.Max(0, cell.Width);
                image.Height = Math.Max(0, cell.Height);
            }
        }

        private void ClearPictures()
        {
            _mosaic.Children.Clear();

            foreach (var bitmap in _shown)
            {
                bitmap.Dispose();
            }

            _shown.Clear();
        }

        private bool PlayVideo(string path)
        {
            if (_player == null)
            {
                _player = new Player { Muted = _muted };
                _player.StateChanged += (state, detail) =>
                {
                    if (state == PlayerState.Ended || state == PlayerState.Failed || state == PlayerState.Protected)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (IsRunning && _playingVideo)
                            {
                                Next();
                            }
                        });
                    }
                };

                _surface.Player = _player;
            }

            _mosaic.IsVisible = false;

            ClearPictures();

            _surface.IsVisible = true;
            _playingVideo = true;

            _player.Play(new MediaRequest { Uri = new Uri(path), IsLive = false, Quiet = true, Label = Path.GetFileName(path) });

            return true;
        }
    }
}
