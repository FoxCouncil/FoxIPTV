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

    /// <summary>The LibVLC side of the main window</summary>
    /// <remarks>
    /// Every LibVLC getter and setter takes the player's locks. The video output needs the UI thread while it re-creates itself at a stream discontinuity, so a call from the UI thread at that moment deadlocks until LibVLC's watchdog gives up, a minute of frozen picture.
    /// So nothing on the UI thread calls into LibVLC: calls go to the thread pool, and what the UI needs to know is kept in fields LibVLC's events keep up to date.
    /// </remarks>
    public partial class MainWindow
    {
        /// <summary>The default volume</summary>
        private const int DefaultVolume = 100;

        private LibVLC _libVlc;

        private MediaPlayer _player;

        /// <summary>Every command to LibVLC goes through this one thread, in the order the UI asked for it</summary>
        /// <remarks>Separate pool jobs could run out of order: a Stop for the new channel landing before the Play for the old one, leaving the old channel playing under the new title.</remarks>
        private readonly BlockingCollection<Action> _vlcCommands = new BlockingCollection<Action>();

        /// <summary>Bumped whenever the media changes, so a track poll for older media stops</summary>
        private int _mediaGeneration;

        /// <summary>The current media's tracks, copied off the UI thread once LibVLC has them; the timer reads this, never the media</summary>
        private volatile MediaTrack[] _currentTracks;

        /// <summary>The current Closed Captioning track id, 0 for the first track</summary>
        private int _ccIdx;

        /// <summary>Used to determine if there is Closed Captioning data available</summary>
        private volatile bool _ccDetected;

        /// <summary>True while the captions come from a subtitle stream of their own (WebVTT beside the video), false when they ride inside the video</summary>
        private volatile bool _ccFromSubtitleStream;

        /// <summary>True while captions are switched off for an ad break</summary>
        /// <remarks>
        /// LibVLC runs a separate subtitle stream beside the video, and each stream crosses an ad join on its own, seconds apart. The first to finish resets the clock both share (PlaylistManager::doDemux, Status::Discontinuity), and the video, still emptying its queue, then fetches nothing: a minute or more of frozen picture, and the next ad never plays.
        /// LibVLC drops a stream whose track is not selected (AbstractStream::doBufferize, "deactivating"), so with captions off through the break only the video crosses the joins. Pluto's ad pieces carry a placeholder subtitle file, so nothing is lost.
        /// </remarks>
        private volatile bool _ccHeldForAds;

        /// <summary>When to bring captions back after a break, 0 when no return is pending; UI thread only</summary>
        private long _ccResumeAtTicks;

        /// <summary>LibVLC fetches a piece up to eight seconds before it reaches the screen, so the last ad is still showing that long after the first programme piece is fetched</summary>
        private static readonly TimeSpan CcResumeDelay = TimeSpan.FromSeconds(8);

        /// <summary>1 while a thread-pool job is asking LibVLC for subtitle tracks, so the TimeChanged callback never asks itself</summary>
        private int _ccProbePending;

        /// <summary>How many times in a row a live stream ended without ever playing, drives the restart back-off</summary>
        private int _endedWithoutPlaying;

        /// <summary>1 while a Play has been handed to LibVLC and it has not yet said Playing or failed, so a second Play is never stacked on it</summary>
        private int _playPending;

        /// <summary>Whether LibVLC has reported Playing and not yet Stopped, Ended or Errored; read this instead of asking LibVLC from the UI thread</summary>
        private volatile bool _isPlaying;

        /// <summary>True once the stream playing turned out to be copy-protected; nothing is retried until the channel changes</summary>
        private volatile bool _isProtected;

        /// <summary>1 once copy protection has been seen in this stream, so it is acted on once</summary>
        private int _protectedSeen;

        /// <summary>LibVLC's dump of an MP4 box tree listing an encrypted video or audio sample entry, the mark of Common Encryption (Widevine, PlayReady)</summary>
        private static readonly Regex EncryptedSampleEntry = new Regex(@"\+ enc[av] size \d+", RegexOptions.Compiled);

        /// <summary>Whether the volume is zero, kept here for the same reason</summary>
        private volatile bool _muted;

        /// <summary>The aspect ratio LibVLC last reported or was last given; null means source</summary>
        private volatile string _aspectRatio;

        /// <summary>The audio channel mode LibVLC last reported or was last given</summary>
        private volatile int _audioChannel;

        /// <summary>Start LibVLC and hook its events</summary>
        private void InitializeVlcPlayer()
        {
            try
            {
                VlcNativeManager.Initialize();

                _libVlc = new LibVLC(VlcNativeManager.Options("--gain=1.8", "--adaptive-logic=highest", "--quiet"));

                // The name is for the sound system; streams see a browser, like every other request FoxIPTV makes
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
                    // Read the volume from the command thread, never from inside LibVLC's own callback
                    var volume = player.Volume;

                    _muted = volume == 0;

                    TvCore.LogInfo($"[Audio] Volume: {volume}");
                });

                _player.Vout += (sender, args) =>
                {
                    // Elsewhere the Direct3D line below never comes, a video output is as close as it gets
                    if (args.Count > 0 && !OperatingSystem.IsWindows())
                    {
                        PlaybackTrace.Picture("video out");
                    }
                };

                HookDiagnostics();

                var worker = new Thread(VlcCommandLoop) { IsBackground = true, Name = "LibVLC commands" };

                worker.Start();

                // The video view hands LibVLC its window only once it has one, which is after the window opens
                Opened += (sender, args) => Avalonia.Threading.Dispatcher.UIThread.Post(AttachVideo, Avalonia.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[.NET] LibVLC failed to start: {ex.Message}");

                StatusMessage.Text = $"LibVLC failed to start: {ex.Message} {VlcNativeManager.HelpMessage()}";
            }

            PlaybackTrace.StatusChanged += status => Ui(() => TraceStatusLabel.Text = status);
        }

        /// <summary>Give the player to the video view once; the one LibVLC call the UI thread makes, before anything plays</summary>
        private void AttachVideo()
        {
            if (_player != null && VideoView.MediaPlayer == null)
            {
                VideoView.MediaPlayer = _player;
            }
        }

        /// <summary>Run LibVLC commands one at a time, in order</summary>
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

        /// <summary>Queue a command for LibVLC</summary>
        /// <param name="command">What to do with the player</param>
        /// <param name="caller">Filled in by the compiler, names the command in the log</param>
        /// <param name="line">Filled in by the compiler, names the command in the log</param>
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

        /// <summary>Handler for TVCore's media changed event, on-demand playback replacing the live channel</summary>
        /// <param name="source">The source to play</param>
        /// <param name="title">The display title</param>
        private void TvCoreOnMediaChanged(MediaSource source, string title)
        {
            Ui(() =>
            {
                TvCore.LogDebug($"[.NET] TvCoreOnMediaChanged({source.Url}, {title})");

                // Whatever was opening is abandoned for this
                _playPending = 0;

                ResetDiagnostics();

                ClearProtected();

                RemoveErrorState();

                ResetCaptions();

                ReleaseMedia();

                // RemoveErrorState stopped the player; the Stopped handler picks up the new media

                Show();
                Activate();

                GuiShow();
            });
        }

        private void ResetCaptions()
        {
            _ccDetected = false;
            _ccFromSubtitleStream = false;
            _ccHeldForAds = false;
            _ccResumeAtTicks = 0;
            CcOptionsButton.IsVisible = false;
        }

        /// <summary>Let go of the media wrapper and the tracks read from it</summary>
        private void ReleaseMedia()
        {
            Interlocked.Increment(ref _mediaGeneration);

            _currentTracks = null;
            _currentTvIconData = null;
        }

        /// <summary>Stop LibVLC off the UI thread; the Stopped event says when it is done</summary>
        private void StopPlayer([System.Runtime.CompilerServices.CallerMemberName] string caller = null)
        {
            Vlc(player => player.Stop(), $"Stop for {caller}");
        }

        /// <summary>Start LibVLC on whatever should be playing: on-demand media if chosen, otherwise the live channel</summary>
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

            Play(channel.Stream, LiveStreamOptions(channel.Stream));
        }

        /// <summary>Hand a stream to LibVLC off the UI thread</summary>
        private void Play(Uri url, string[] options)
        {
            var libVlc = _libVlc;

            TvCore.LogInfo($"[.NET] Play {url} with options [{string.Join(" ", options)}]");

            Vlc(player =>
            {
                // The player keeps its own reference to the media, ours goes as soon as it is handed over
                using (var media = new Media(libVlc, url, options))
                {
                    player.Play(media);
                }
            });
        }

        /// <summary>LibVLC media options for a live channel</summary>
        /// <param name="stream">The channel's stream address</param>
        /// <returns>LibVLC option strings</returns>
        /// <remarks>
        /// A live HLS piece cannot be fetched before it is published, so the picture must run further behind the newest piece than one piece is long, or every piece arrives late.
        /// LibVLC starts 1 second behind and learns this the hard way: on Pluto's 5 second pieces it logged "PCR is called too late" seven times in four minutes, raising its own delay 1000, 1782, 1931, 4035, 4206, 4398, 5325 ms, each rise a reset of the clock and a hitch on screen, with dropped frames between. Once past 5 seconds the log went quiet.
        /// Starting at 8 seconds covers 6 second pieces as well. The playlist already lists more than that when a channel opens, so the picture comes up no later.
        /// </remarks>
        private static string[] LiveStreamOptions(Uri stream)
        {
            if (stream.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || stream.AbsolutePath.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { ":network-caching=8000" };
            }

            return Array.Empty<string>();
        }

        /// <summary>Turn HTTP headers a source needs into LibVLC media options</summary>
        /// <param name="headers">The headers</param>
        /// <returns>LibVLC option strings</returns>
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

        /// <summary>Copy LibVLC's own messages to the log and pick the ones that mark a playback stage</summary>
        /// <param name="message">The LibVLC message</param>
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
                // "webvtt" is a subtitle stream of its own, "cc" rides inside the video
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

        /// <summary>The LibVLC TimeChanged event handler</summary>
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
                // Ask LibVLC about subtitle tracks from the command thread, never from inside its own callback
                Vlc(player =>
                {
                    try
                    {
                        if (!_ccDetected && player.SpuCount != 0)
                        {
                            _ccDetected = true;

                            Ui(GuiShow);

                            ProcessClosedCaptioning(player);
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

            // Apply the audio channel and aspect ratio once per play rather than checking them ten times a second
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

        /// <summary>LibVLC fills the track list a little after playback starts; poll for it off the UI thread, then hand the UI a copy</summary>
        private void PollTracks(MediaPlayer player, int generation)
        {
            // This job's own wrapper, released here and nowhere else
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

        /// <summary>Forget any copy protection seen, a new stream is starting</summary>
        private void ClearProtected()
        {
            _isProtected = false;

            Interlocked.Exchange(ref _protectedSeen, 0);
        }

        /// <summary>The stream is copy-protected: LibVLC has no keys, so it would buffer scrambled data for ever. Stop, say so, and take a live channel out of the list for good</summary>
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
                    // On-demand media has a real end, live channels are restarted
                    PlayerStatusLabel.Text = "Ended";

                    return;
                }

                if (_isProtected)
                {
                    return;
                }

                PlayerStatusLabel.Text = "Buffering";

                // A live stream that ends at once is broken, not finished: retry, but slower each time and not forever
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

        /// <summary>Pick the caption track the settings ask for; runs on the command thread</summary>
        private void ProcessClosedCaptioning(MediaPlayer player)
        {
            // Off for the length of an ad break whatever the setting says: see _ccHeldForAds
            var held = _ccHeldForAds;
            var enabled = TvCore.Settings.CCEnabled && !held;

            var all = player.SpuDescription;

            if (all.Length == 0)
            {
                return;
            }

            // The first entry is always "Disable"
            var chosen = _ccIdx != 0 && !held && all.Any(x => x.Id == _ccIdx) ? all.First(x => x.Id == _ccIdx) : all[Math.Min(enabled ? 1 : 0, all.Length - 1)];

            TvCore.LogInfo($"[CC] Tracks: {string.Join(", ", all.Select(x => $"{x.Id}={x.Name}"))}; setting on {TvCore.Settings.CCEnabled}, held {held}, picked {_ccIdx}; choosing {chosen.Id}={chosen.Name}");

            player.SetSpu(chosen.Id);

            if (enabled && all.Length > 2)
            {
                // Read the track list here on the pool; the UI thread only gets copies
                var currentName = chosen.Name;
                var subIdx = _ccIdx != 0 ? 1 : 2;
                var tracks = all.Skip(subIdx).Where(x => x.Id != _ccIdx).Select(x => new KeyValuePair<int, string>(x.Id, x.Name)).ToList();

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

        /// <summary>Switch captions off when an ad break starts and back on once the programme is on screen again: see <see cref="_ccHeldForAds"/></summary>
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

        /// <summary>Stop LibVLC, let go of it, then shut the application down</summary>
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

            // No more commands; the stop runs after anything already queued, off the UI thread, see the class remarks
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
