// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Avalonia.Controls;
    using Avalonia.Media.Imaging;
    using Avalonia.Threading;
    using Classes;
    using Playback;
    using Playback.Video;

    public sealed class AdReel : IDisposable
    {
        private const double PictureSeconds = 8;

        private static readonly HashSet<string> PictureTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

        private static readonly HashSet<string> VideoTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".m4v", ".mkv", ".mov", ".webm", ".avi", ".wmv", ".ts" };

        private readonly Image _image;

        private readonly VideoSurface _surface;

        private readonly Queue<string> _queue = new Queue<string>();

        private readonly Random _random = new Random();

        private Player _player;

        private Bitmap _bitmap;

        private string _folder;

        private DateTime _pictureUntil;

        private bool _playingVideo;

        private bool _muted;

        public AdReel(Image image, VideoSurface surface)
        {
            _image = image;
            _surface = surface;
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

        public static bool HasMedia(string folder)
        {
            return Files(folder).Any();
        }

        public bool Start(string folder)
        {
            _folder = folder;
            _queue.Clear();

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
            _queue.Clear();

            _player?.Stop();

            _surface.IsVisible = false;
            _image.IsVisible = false;
            _image.Source = null;

            _bitmap?.Dispose();
            _bitmap = null;
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

        private static IEnumerable<string> Files(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return Enumerable.Empty<string>();
            }

            try
            {
                return Directory.EnumerateFiles(folder).Where(x => PictureTypes.Contains(Path.GetExtension(x)) || VideoTypes.Contains(Path.GetExtension(x))).ToList();
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Ads] Reading the ad media folder failed: {ex.Message}");

                return Enumerable.Empty<string>();
            }
        }

        private bool Refill()
        {
            var files = Files(_folder).OrderBy(x => _random.Next()).ToList();

            foreach (var file in files)
            {
                _queue.Enqueue(file);
            }

            return files.Count > 0;
        }

        private void Next()
        {
            for (var tries = 0; IsRunning && tries < 32; tries++)
            {
                if (_queue.Count == 0 && !Refill())
                {
                    Stop();

                    return;
                }

                var path = _queue.Dequeue();

                if (PictureTypes.Contains(Path.GetExtension(path)) ? ShowPicture(path) : PlayVideo(path))
                {
                    ItemShown?.Invoke();

                    return;
                }
            }

            Stop();
        }

        private bool ShowPicture(string path)
        {
            Bitmap bitmap;

            try
            {
                bitmap = new Bitmap(path);
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Ads] Could not show {Path.GetFileName(path)}: {ex.Message}");

                return false;
            }

            _player?.Stop();
            _playingVideo = false;
            _surface.IsVisible = false;

            var old = _bitmap;

            _bitmap = bitmap;
            _image.Source = bitmap;
            _image.IsVisible = true;

            old?.Dispose();

            _pictureUntil = DateTime.UtcNow.AddSeconds(PictureSeconds);

            return true;
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

            _image.IsVisible = false;
            _image.Source = null;
            _bitmap?.Dispose();
            _bitmap = null;

            _surface.IsVisible = true;
            _playingVideo = true;

            _player.Play(new MediaRequest { Uri = new Uri(path), IsLive = false, Quiet = true, Label = Path.GetFileName(path) });

            return true;
        }
    }
}
