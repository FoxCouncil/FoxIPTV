// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
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

        private Task<List<string>> _files;

        private Task<List<Bitmap>> _nextSet;

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

        public void Prepare(string folder)
        {
            if (_files != null && string.Equals(folder, _folder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _folder = folder;
            _pictures.Clear();
            _videos.Clear();
            var files = string.IsNullOrEmpty(folder) ? Task.FromResult(new List<string>()) : Task.Run(() => MediaFolder.Files(folder));

            _files = files;

            DropNextSet();

            files.ContinueWith(task => Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_files, files))
                {
                    PreloadNextSet();
                }
            }));
        }

        public bool Start(string folder)
        {
            Prepare(folder);

            if (_files.IsCompleted && _files.Result.Count == 0)
            {
                return false;
            }

            IsRunning = true;
            _pictureTurns = 0;
            _pictureUntil = DateTime.MinValue;

            Advance();

            return IsRunning;
        }

        public void Stop()
        {
            IsRunning = false;
            _playingVideo = false;

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
                Advance();
            }
        }

        public void Dispose()
        {
            Stop();
            DropNextSet();

            _player?.Dispose();
            _player = null;
        }

        private bool Refill()
        {
            if (_files == null || !_files.IsCompleted)
            {
                return false;
            }

            var files = _files.Result.OrderBy(x => _random.Next()).ToList();

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

        private void Advance()
        {
            if (!Refill())
            {
                if (_files != null && _files.IsCompleted)
                {
                    Stop();
                }

                return;
            }

            var hasPictures = _pictures.Count > 0 || _nextSet != null;
            var videoTurn = _videos.Count > 0 && (!hasPictures || _pictureTurns >= 2);

            if (videoTurn)
            {
                _pictureTurns = 0;

                PlayVideo(_videos.Dequeue());

                ItemShown?.Invoke();

                return;
            }

            PreloadNextSet();

            if (_nextSet == null || !_nextSet.IsCompleted)
            {
                return;
            }

            var bitmaps = _nextSet.IsCompletedSuccessfully ? _nextSet.Result : new List<Bitmap>();

            _nextSet = null;

            PreloadNextSet();

            if (bitmaps.Count == 0)
            {
                return;
            }

            ShowPictures(bitmaps);

            _pictureTurns++;

            ItemShown?.Invoke();
        }

        private void PreloadNextSet()
        {
            if (_nextSet != null || !Refill() || _pictures.Count == 0)
            {
                return;
            }

            var count = Math.Min(_pictures.Count, _random.Next(3, 7));
            var paths = new List<string>();

            for (var i = 0; i < count; i++)
            {
                paths.Add(_pictures.Dequeue());
            }

            _nextSet = Task.Run(() => Decode(paths));
        }

        private static List<Bitmap> Decode(List<string> paths)
        {
            var bitmaps = new List<Bitmap>();

            foreach (var path in paths)
            {
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

            return bitmaps;
        }

        private void DropNextSet()
        {
            var pending = _nextSet;

            _nextSet = null;

            pending?.ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    foreach (var bitmap in task.Result)
                    {
                        bitmap.Dispose();
                    }
                }
            });
        }

        private void ShowPictures(List<Bitmap> bitmaps)
        {
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

        private void PlayVideo(string path)
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
                                _playingVideo = false;
                                _pictureUntil = DateTime.MinValue;

                                Advance();
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
        }
    }
}
