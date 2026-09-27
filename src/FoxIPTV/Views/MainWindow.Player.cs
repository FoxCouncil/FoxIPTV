// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Classes;
    using LibVLCSharp.Shared;

    public partial class MainWindow
    {
        /// <summary>The default volume</summary>
        private const int DefaultVolume = 100;

        private LibVLC _libVlc;

        private MediaPlayer _player;

        private readonly BlockingCollection<Action> _vlcCommands = new BlockingCollection<Action>();

        private int _mediaGeneration;

        private volatile MediaTrack[] _currentTracks;

        private int _ccIdx;

        /// <summary>Used to determine if there is Closed Captioning data available</summary>
        private volatile bool _ccDetected;

        private volatile bool _ccAvailable;

        private volatile bool _ccFromSubtitleStream;

        private volatile bool _ccHeldForAds;

        private long _ccResumeAtTicks;

        private static readonly TimeSpan CcResumeDelay = TimeSpan.FromSeconds(8);

        private int _ccProbePending;

        private int _endedWithoutPlaying;

        private int _playPending;

        private volatile bool _isPlaying;

        private volatile bool _isProtected;

        private int _protectedSeen;

        private static readonly Regex EncryptedSampleEntry = new Regex(@"\+ enc[av] size \d+", RegexOptions.Compiled);

        private volatile bool _muted;

        private volatile string _aspectRatio;

        private volatile int _audioChannel;

        private void InitializeVlcPlayer()
        {
            try
            {
                VlcNativeManager.Initialize();

                _libVlc = new LibVLC(VlcNativeManager.Options("--gain=1.8", "--adaptive-logic=highest", "--quiet"));

                _libVlc.SetUserAgent("Fox IPTV", Web.UserAgent);
                _libVlc.SetAppId("FoxIPTV", TvCore.Version, string.Empty);

                _libVlc.Log += (sender, args) => OnVlcLog(args.Message, args.Level, args.Module);

                _player = new MediaPlayer(_libVlc);

                _player.Playing += VlcPlayer_Playing;
                _player.Paused += (sender, args) => Ui(() => PlayerStatusLabel.Text = "Paused");
                _player.Stopped += VlcPlayer_Stopped;
                _player.Buffering += VlcPlayer_Buffering;
                _player.EncounteredError += VlcPlayer_EncounteredError;
                _player.EndReached += VlcPlayer_EndReached;
                _player.TimeChanged += VlcPlayer_TimeChanged;
                _player.VolumeChanged += (sender, args) => Vlc(player =>
                {
                    var volume = player.Volume;

                    _muted = volume == 0;

                    TvCore.LogInfo($"[Audio] Volume: {volume}");
                });

                _player.Vout += (sender, args) =>
                {
                    if (args.Count > 0 && !OperatingSystem.IsWindows())
                    {
                        PlaybackTrace.Picture("video out");
                    }
                };

                HookDiagnostics();

                var worker = new Thread(VlcCommandLoop) { IsBackground = true, Name = "LibVLC commands" };

                worker.Start();

                Opened += (sender, args) => Avalonia.Threading.Dispatcher.UIThread.Post(AttachVideo, Avalonia.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[.NET] LibVLC failed to start: {ex.Message}");

                StatusMessage.Text = $"LibVLC failed to start: {ex.Message} {VlcNativeManager.HelpMessage()}";
            }

            PlaybackTrace.StatusChanged += status => Ui(() => TraceStatusLabel.Text = status);
        }

        private void AttachVideo()
        {
            if (_player != null && VideoView.MediaPlayer == null)
            {
                VideoView.MediaPlayer = _player;
            }
        }

        private void VlcCommandLoop()
        {
            foreach (var command in _vlcCommands.GetConsumingEnumerable())
            {
                try
                {
                    command();
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[.NET] LibVLC command failed: {ex.Message}");
                }
            }
        }

        private void Vlc(Action<MediaPlayer> command, [System.Runtime.CompilerServices.CallerMemberName] string caller = null, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            var player = _player;
            var name = $"{caller}:{line}";

            if (player == null || _vlcCommands.IsAddingCompleted)
            {
                TvCore.LogInfo($"[VLC cmd] dropped {name}, {(player == null ? "no player" : "shutting down")}");

                return;
            }

            try
            {
                _vlcCommands.Add(WrapCommand(player, command, name));
            }
            catch (InvalidOperationException)
            {
                TvCore.LogInfo($"[VLC cmd] dropped {name}, shutting down");
            }
        }

        /// <summary>Handler for TVCore's channel changed event</summary>
        /// <param name="channel">The new channel we are changing to</param>
        private void TvCoreOnChannelChanged(uint channel)
        {
            Ui(() =>
            {
                TvCore.LogDebug($"[.NET] TvCoreOnChannelChanged({channel})");

                PlaybackTrace.Begin($"channel {channel} {TvCore.CurrentChannel?.Name}");
                PlaybackTrace.Mark("stop requested");

                StreamFacts.Reset();
                AdDetector.Reset();
                ResetDiagnostics();
                _playPending = 0;

                ClearProtected();

                _endedWithoutPlaying = 0;

                RemoveErrorState();

                TvCore.Settings.Channel = channel;
                TvCore.Settings.Save();

                ResetCaptions();

                ReleaseMedia();

                GuiShow();
            });
        }

        private void TvCoreOnMediaChanged(MediaSource source, string title)
        {
            Ui(() =>
            {
                TvCore.LogDebug($"[.NET] TvCoreOnMediaChanged({source.Url}, {title})");

                _playPending = 0;

                ResetDiagnostics();

                ClearProtected();

                RemoveErrorState();

                ResetCaptions();

                ReleaseMedia();

                Show();
                Activate();

                GuiShow();
            });
        }

        private void ResetCaptions()
        {
            _ccDetected = false;
            _ccAvailable = false;
            _ccFromSubtitleStream = false;
            _ccHeldForAds = false;
            _ccResumeAtTicks = 0;
            CcOptionsButton.IsVisible = false;
        }

        private void ReleaseMedia()
        {
            Interlocked.Increment(ref _mediaGeneration);

            _currentTracks = null;
            _currentTvIconData = null;
        }

        private void StopPlayer([System.Runtime.CompilerServices.CallerMemberName] string caller = null)
        {
            Vlc(player => player.Stop(), $"Stop for {caller}");
        }

        private void PlayCurrent()
        {
            if (_player == null)
            {
                return;
            }

            AttachVideo();

            if (Interlocked.Exchange(ref _playPending, 1) == 1)
            {
                TvCore.LogInfo("[.NET] PlayCurrent(): a play is already in flight, ignoring");

                return;
            }

            var media = TvCore.CurrentMedia;

            if (media != null)
            {
                Play(media.Url, BuildVlcOptions(media.Headers));

                return;
            }

            var channel = TvCore.CurrentChannel;

            if (channel == null)
            {
                _playPending = 0;

                return;
            }

            PlaybackTrace.Mark("play", channel.Stream.Host);

            Play(channel.Stream, Array.Empty<string>());
        }

        private void Play(Uri url, string[] options)
        {
            var libVlc = _libVlc;

            TvCore.LogInfo($"[.NET] Play {url} with options [{string.Join(" ", options)}]");

            Vlc(player =>
            {
                using (var media = new Media(libVlc, url, options))
                {
                    player.Play(media);
                }
            });
        }

        private static string[] BuildVlcOptions(Dictionary<string, string> headers)
        {
            var options = new List<string>();

            if (headers == null)
            {
                return options.ToArray();
            }

            foreach (var header in headers)
            {
                if (string.Equals(header.Key, "User-Agent", StringComparison.OrdinalIgnoreCase))
                {
                    options.Add($":http-user-agent={header.Value}");
                }
                else if (string.Equals(header.Key, "Referer", StringComparison.OrdinalIgnoreCase) || string.Equals(header.Key, "Referrer", StringComparison.OrdinalIgnoreCase))
                {
                    options.Add($":http-referrer={header.Value}");
                }
            }

            return options.ToArray();
        }

        private void OnVlcLog(string message, LogLevel level = LogLevel.Debug, string module = null)
        {
            if (message == null)
            {
                return;
            }

            TvCore.LogInfo(level == LogLevel.Debug ? $"[Media] {message}" : $"[Media {level}{(module == null ? string.Empty : " " + module)}] {message}");

            StreamFacts.Observe(message);
            AdDetector.Observe(message);
            NotePiece(message);

            if (message.StartsWith("using spu decoder module", StringComparison.Ordinal))
            {
                _ccFromSubtitleStream = message.IndexOf("\"webvtt\"", StringComparison.Ordinal) >= 0;
            }

            if (EncryptedSampleEntry.IsMatch(message) && Interlocked.Exchange(ref _protectedSeen, 1) == 0)
            {
                PlaybackTrace.Mark("copy-protected");

                Ui(OnProtectedStream);
            }

            if (message.StartsWith("creating access: http", StringComparison.Ordinal))
            {
                if (message.IndexOf("127.0.0.1", StringComparison.Ordinal) < 0 && Uri.TryCreate(message.Substring("creating access: ".Length).Trim(), UriKind.Absolute, out var url))
                {
                    PlaybackTrace.MarkOnce("first cdn request", url.Host);
                }
            }
            else if (message.StartsWith("TLS handshake: Success", StringComparison.Ordinal))
            {
                PlaybackTrace.MarkOnce("tls up");
            }
            else if (message.StartsWith("Buffering 100%", StringComparison.Ordinal))
            {
                PlaybackTrace.MarkOnce("buffered");
            }
            else if (message.StartsWith("D3D11 pool succeed", StringComparison.Ordinal))
            {
                var size = Regex.Match(message, @"\((\d+x\d+)\)");

                PlaybackTrace.Picture(size.Success ? size.Groups[1].Value : "picture");
            }
            else if (message.StartsWith("EOF reached", StringComparison.Ordinal))
            {
                PlaybackTrace.Mark("eof");
            }
        }

        private void VlcPlayer_TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        {
            if (_isClosing)
            {
                return;
            }

            PlaybackTrace.MarkOnce("clock running");

            NoteClock(e.Time);

            _endedWithoutPlaying = 0;

            if (!_ccDetected && _isPlaying && Interlocked.Exchange(ref _ccProbePending, 1) == 0)
            {
                Vlc(player =>
                {
                    try
                    {
                        if (!_ccDetected && player.SpuCount != 0)
                        {
                            _ccDetected = true;

                            ProcessClosedCaptioning(player);

                            Ui(GuiShow);
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _ccProbePending, 0);
                    }
                });
            }

            var time = TimeSpan.FromMilliseconds(e.Time);

            Ui(() =>
            {
                if (_isClosing)
                {
                    return;
                }

                TimeStatusLabel.Text = $"{time.Minutes:00}:{time.Seconds:00}";
            });
        }

        /// <summary>The LibVLC Playing event handler</summary>
        private void VlcPlayer_Playing(object sender, EventArgs e)
        {
            TvCore.LogDebug("[.NET] VlcPlayer_Playing()");

            _playPending = 0;

            PlaybackTrace.Mark("vlc playing");

            _isPlaying = true;

            var generation = Volatile.Read(ref _mediaGeneration);
            var player = _player;

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    PollTracks(player, generation);
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[.NET] Track poll failed: {ex.Message}");
                }
            });

            Vlc(p =>
            {
                if ((int)p.Channel != TvCore.Settings.StereoMode)
                {
                    p.SetChannel((AudioOutputChannel)TvCore.Settings.StereoMode);
                }

                var wanted = string.IsNullOrEmpty(TvCore.Settings.AspectRatio) ? null : TvCore.Settings.AspectRatio;

                if ((p.AspectRatio ?? string.Empty) != (wanted ?? string.Empty))
                {
                    p.AspectRatio = wanted;
                }

                _muted = p.Volume == 0;
                _aspectRatio = p.AspectRatio;
                _audioChannel = (int)p.Channel;
            });

            Ui(() => PlayerStatusLabel.Text = "Playing");
        }

        private void PollTracks(MediaPlayer player, int generation)
        {
            using (var media = player.Media)
            {
                for (var attempt = 0; attempt < 60 && media != null && generation == Volatile.Read(ref _mediaGeneration) && _isPlaying; attempt++)
                {
                    var tracks = media.Tracks;

                    if (tracks != null && tracks.Length > 0)
                    {
                        if (generation == Volatile.Read(ref _mediaGeneration))
                        {
                            _currentTracks = tracks;
                        }

                        break;
                    }

                    Thread.Sleep(500);
                }
            }
        }

        private void ClearProtected()
        {
            _isProtected = false;

            Interlocked.Exchange(ref _protectedSeen, 0);
        }

        private void OnProtectedStream()
        {
            if (_isProtected || _isClosing)
            {
                return;
            }

            _isProtected = true;
            _isErrorState = false;
            _playPending = 0;

            var live = TvCore.CurrentMedia == null;

            TvCore.LogInfo($"[.NET] Copy-protected stream: {(live ? TvCore.CurrentChannel?.Name : TvCore.CurrentMediaTitle)}");

            StopPlayer();

            if (live)
            {
                TvCore.MarkProtected(TvCore.CurrentChannel);
            }

            StatusMessage.Text = live ? "This channel is copy-protected and can't be played. It has been taken out of the channel list." : "This is copy-protected and can't be played.";
            StatusMessageBox.IsVisible = true;

            PlayerStatusLabel.Text = "Protected";
            PlaybackTrace.SetStatus("Copy-protected, can't be played");
        }

        /// <summary>The LibVLC Stopped event handler</summary>
        private void VlcPlayer_Stopped(object sender, EventArgs e)
        {
            TvCore.LogDebug("[.NET] VlcPlayer_Stopped()");

            _isPlaying = false;

            PlaybackTrace.Mark("stopped");

            Ui(() =>
            {
                if (_isClosing)
                {
                    return;
                }

                PlayerStatusLabel.Text = "Buffering";

                if (!_isErrorState && !_isProtected)
                {
                    PlayCurrent();
                }
            });
        }

        /// <summary>The LibVLC Buffering event handler</summary>
        private void VlcPlayer_Buffering(object sender, MediaPlayerBufferingEventArgs e)
        {
            PlaybackTrace.SetStatus($"Buffering {(int)e.Cache}%");

            Ui(() => BufferStatusProgressBar.Value = e.Cache);
        }

        /// <summary>The LibVLC EncounteredError event handler</summary>
        private void VlcPlayer_EncounteredError(object sender, EventArgs e)
        {
            TvCore.LogError("[.NET] VlcPlayer_EncounteredError()");

            _isPlaying = false;

            _playPending = 0;

            PlaybackTrace.Mark("vlc error");

            Ui(() =>
            {
                if (_isProtected)
                {
                    return;
                }

                SetErrorState();
                PlayerStatusLabel.Text = "Error";
            });
        }

        /// <summary>The LibVLC EndReached event handler</summary>
        private void VlcPlayer_EndReached(object sender, EventArgs e)
        {
            TvCore.LogDebug("[.NET] VlcPlayer_EndReached()");

            _isPlaying = false;

            _playPending = 0;

            PlaybackTrace.Mark("end reached");

            Ui(() =>
            {
                if (TvCore.CurrentMedia != null)
                {
                    PlayerStatusLabel.Text = "Ended";

                    return;
                }

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
            });
        }

        private void ProcessClosedCaptioning(MediaPlayer player)
        {
            var held = _ccHeldForAds;
            var enabled = TvCore.Settings.CCEnabled && !held;

            var all = player.SpuDescription;

            if (all.Length == 0)
            {
                return;
            }

            var inVideo = new HashSet<int>();
            var webVtt = new HashSet<int>();

            using (var media = player.Media)
            {
                foreach (var track in media?.Tracks ?? Array.Empty<MediaTrack>())
                {
                    var codec = track.Codec.ToFourCC().ToLowerInvariant();

                    if (track.TrackType == TrackType.Text && (codec == "c608" || codec == "c708"))
                    {
                        inVideo.Add(track.Id);
                    }
                    else if (track.TrackType == TrackType.Text && codec == "wvtt")
                    {
                        webVtt.Add(track.Id);
                    }
                }
            }

            var choices = all.Where(x => x.Id != -1 && !webVtt.Contains(x.Id)).ToList();

            _ccAvailable = choices.Count > 0;

            var preferred = choices.Where(x => inVideo.Contains(x.Id) || (x.Name ?? string.Empty).StartsWith("Closed captions", StringComparison.Ordinal)).Concat(choices).ToList();

            var chosen = _ccIdx != 0 && !held && choices.Any(x => x.Id == _ccIdx) ? choices.First(x => x.Id == _ccIdx) : enabled && preferred.Count > 0 ? preferred[0] : all[0];

            TvCore.LogInfo($"[CC] Tracks: {string.Join(", ", all.Select(x => $"{x.Id}={x.Name}"))}; setting on {TvCore.Settings.CCEnabled}, held {held}, picked {_ccIdx}; choosing {chosen.Id}={chosen.Name}");

            player.SetSpu(chosen.Id);

            if (enabled && choices.Count > 1)
            {
                var currentName = chosen.Name;
                var tracks = choices.Where(x => x.Id != chosen.Id).Select(x => new KeyValuePair<int, string>(x.Id, x.Name)).ToList();

                Ui(() =>
                {
                    CcOptionsButton.IsVisible = true;
                    CcOptionsButton.Content = currentName;
                    ((MenuFlyout)CcOptionsButton.Flyout).Items.Clear();

                    foreach (var subTitle in tracks)
                    {
                        var item = new MenuItem { Header = subTitle.Value, Tag = subTitle.Key };

                        item.Click += (sender, args) =>
                        {
                            _ccIdx = (int)((MenuItem)sender).Tag;
                            _ccDetected = false;
                        };

                        ((MenuFlyout)CcOptionsButton.Flyout).Items.Add(item);
                    }
                });
            }
            else
            {
                Ui(() => CcOptionsButton.IsVisible = false);
            }
        }

        private void TimerCaptionHold()
        {
            if (AdDetector.InAd)
            {
                _ccResumeAtTicks = 0;

                if (!_ccHeldForAds && _ccFromSubtitleStream && TvCore.Settings.CCEnabled && _isPlaying)
                {
                    _ccHeldForAds = true;

                    TvCore.LogInfo("[CC] Ad break started, captions off until it ends");

                    Vlc(ProcessClosedCaptioning);
                }

                return;
            }

            if (!_ccHeldForAds)
            {
                return;
            }

            if (_ccResumeAtTicks == 0)
            {
                _ccResumeAtTicks = DateTime.UtcNow.Add(CcResumeDelay).Ticks;

                return;
            }

            if (DateTime.UtcNow.Ticks < _ccResumeAtTicks)
            {
                return;
            }

            _ccHeldForAds = false;
            _ccResumeAtTicks = 0;

            TvCore.LogInfo("[CC] Ad break over, captions back on");

            Vlc(ProcessClosedCaptioning);
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

            var player = _player;

            _vlcCommands.CompleteAdding();

            await Task.Run(() =>
            {
                try
                {
                    while (_vlcCommands.TryTake(out var pending))
                    {
                        pending();
                    }

                    player?.Stop();
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
