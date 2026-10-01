// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Threading.Tasks;
    using Classes;
    using Playback;
    using Services;

    public partial class MainWindow
    {
        private readonly Player _player = new Player();

        private int _endedWithoutPlaying;

        private volatile bool _isPlaying;

        private volatile bool _isProtected;

        private volatile bool _ccAvailable;

        private string _caption;

        private string _aspectRatio;

        private int _audioChannel;

        private bool _pictureTraced;

        private void InitializePlayer()
        {
            if (!FFmpegNative.Initialize())
            {
                StatusMessage.Text = $"FFmpeg failed to load: {FFmpegNative.Failure}. Build it with build/ffmpeg.sh.";
            }

            AudioOutput.Initialize();

            _player.StereoMode = Enum.IsDefined(typeof(StereoMode), TvCore.Settings.StereoMode) ? (StereoMode)TvCore.Settings.StereoMode : StereoMode.Stereo;
            _audioChannel = (int)_player.StereoMode;

            _aspectRatio = string.IsNullOrEmpty(TvCore.Settings.AspectRatio) ? null : TvCore.Settings.AspectRatio;

            VideoView.Player = _player;
            VideoView.AspectRatio = TvCore.Settings.AspectRatio;
            VideoView.PictureShown += OnPictureShown;

            _player.StateChanged += (state, detail) => Ui(() => OnPlayerState(state, detail));
            _player.InfoChanged += info => Ui(() => OnStreamInfo(info));
            _player.CaptionChanged += caption => Ui(() => OnCaption(caption));
            _player.BufferChanged += percent => Ui(() => BufferStatusProgressBar.Value = percent);

            PlaybackTrace.StatusChanged += status => Ui(() => TraceStatusLabel.Text = status);
        }

        /// <summary>Handler for TVCore's channel changed event</summary>
        /// <param name="channel">The new channel we are changing to</param>
        private void TvCoreOnChannelChanged(uint channel)
        {
            Ui(() =>
            {
                TvCore.LogDebug($"[.NET] TvCoreOnChannelChanged({channel})");

                PlaybackTrace.Begin($"channel {channel} {TvCore.CurrentChannel?.Name}");

                AdDetector.Reset();
                ResetDiagnostics();
                ClearProtected();

                _endedWithoutPlaying = 0;

                RemoveErrorState();

                TvCore.Settings.Channel = channel;
                TvCore.Settings.Save();

                ResetCaptions();

                ReleaseMedia();

                GuiShow();

                PlayCurrent();
            });
        }

        private void ResetCaptions()
        {
            _ccAvailable = false;
            _caption = null;

            CcOptionsButton.IsVisible = false;
            CaptionBox.IsVisible = false;
        }

        private void ReleaseMedia()
        {
            _currentTvIconData = null;
            _isPlaying = false;
        }

        private void StopPlayer()
        {
            _isPlaying = false;

            _player.Stop();
        }

        private async void PlayCurrent()
        {
            if (_isClosing)
            {
                return;
            }

            var request = CurrentRequest();

            if (request == null)
            {
                return;
            }

            var channel = TvCore.CurrentChannel;

            if (channel != null && TvCore.CurrentService is ILiveTuner tuner)
            {
                try
                {
                    var fresh = await tuner.Tune(channel);

                    if (_isClosing || !ReferenceEquals(TvCore.CurrentChannel, channel))
                    {
                        return;
                    }

                    if (fresh != null)
                    {
                        request.Uri = fresh;
                    }
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[.NET] Tuning {channel.Name} failed, playing its stored address: {ex.Message}");
                }
            }

            TvCore.LogInfo($"[.NET] Play {request.Uri}");

            _pictureTraced = false;

            VideoView.Clear();

            _player.Play(request);

            VideoView.Wake();
        }

        private static MediaRequest CurrentRequest()
        {
            var channel = TvCore.CurrentChannel;

            if (channel?.Stream == null)
            {
                return null;
            }

            PlaybackTrace.Mark("play", channel.Stream.Host);

            return new MediaRequest { Uri = channel.Stream, IsLive = true, Label = $"{channel.Index} {channel.Name}" };
        }

        private void OnPictureShown(int width, int height)
        {
            if (_pictureTraced)
            {
                return;
            }

            _pictureTraced = true;

            PlaybackTrace.Picture($"{width}x{height}");
        }

        private void OnPlayerState(PlayerState state, string detail)
        {
            if (_isClosing)
            {
                return;
            }

            switch (state)
            {
                case PlayerState.Opening:
                {
                    PlayerStatusLabel.Text = "Opening";
                }
                break;

                case PlayerState.Buffering:
                {
                    PlayerStatusLabel.Text = "Buffering";
                    PlaybackTrace.SetStatus("Buffering");
                }
                break;

                case PlayerState.Playing:
                {
                    _isPlaying = true;
                    _endedWithoutPlaying = 0;

                    PlayerStatusLabel.Text = "Playing";

                    VideoView.Wake();
                }
                break;

                case PlayerState.Ended:
                {
                    OnEnded();
                }
                break;

                case PlayerState.Failed:
                {
                    _isPlaying = false;

                    if (_isProtected)
                    {
                        return;
                    }

                    TvCore.LogError($"[.NET] Playback failed: {detail}");

                    PlaybackTrace.SetStatus(detail ?? "Stream error");

                    SetErrorState();
                    PlayerStatusLabel.Text = "Error";
                }
                break;

                case PlayerState.Protected:
                {
                    OnProtectedStream();
                }
                break;
            }
        }

        private void OnStreamInfo(StreamInfo info)
        {
            if (info.Captions && !_ccAvailable)
            {
                _ccAvailable = true;

                TvCore.LogInfo("[CC] Captions found in the video");
            }

            if (_uiFadeoutTime > 0)
            {
                TimerTvIcons();
            }
        }

        private void OnCaption(string caption)
        {
            _caption = caption;

            ShowCaption();
        }

        private void ShowCaption()
        {
            var show = TvCore.Settings.CCEnabled && !string.IsNullOrEmpty(_caption);

            CaptionLabel.Text = show ? _caption : string.Empty;
            CaptionBox.IsVisible = show;
        }

        private void ClearProtected()
        {
            _isProtected = false;
        }

        private void OnProtectedStream()
        {
            if (_isProtected || _isClosing)
            {
                return;
            }

            _isProtected = true;
            _isErrorState = false;
            _isPlaying = false;

            TvCore.LogInfo($"[.NET] Copy-protected stream: {TvCore.CurrentChannel?.Name}");

            StopPlayer();

            TvCore.MarkProtected(TvCore.CurrentChannel);

            StatusMessage.Text = "This channel is copy-protected and can't be played. It has been taken out of the channel list.";
            StatusMessageBox.IsVisible = true;

            PlayerStatusLabel.Text = "Protected";
            PlaybackTrace.SetStatus("Copy-protected, can't be played");
        }

        private void OnEnded()
        {
            _isPlaying = false;

            if (_isProtected)
            {
                return;
            }

            PlayerStatusLabel.Text = "Buffering";

            _endedWithoutPlaying++;

            if (_endedWithoutPlaying > 6)
            {
                TvCore.LogError("[.NET] Stream ended six times without playing, giving up");

                PlaybackTrace.SetStatus("Stream keeps ending, stopped retrying");

                SetErrorState();
                PlayerStatusLabel.Text = "Error";

                return;
            }

            var delay = TimeSpan.FromSeconds(Math.Min(10, 2 * _endedWithoutPlaying));

            PlaybackTrace.SetStatus($"Stream ended, retrying in {delay.TotalSeconds:0}s (try {_endedWithoutPlaying})");

            Task.Delay(delay).ContinueWith(task => Ui(() =>
            {
                if (!_isErrorState && !_isProtected && !_isClosing)
                {
                    PlayCurrent();
                }
            }));
        }

        private async void Quit()
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;

            TvCore.Settings.Save();

            _timer.Stop();

            await Task.Run(() =>
            {
                try
                {
                    _player.Dispose();
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[.NET] Quit(): {ex.Message}");
                }
            });

            _trayIcon?.Dispose();

            App.Desktop?.Shutdown();
        }
    }
}
