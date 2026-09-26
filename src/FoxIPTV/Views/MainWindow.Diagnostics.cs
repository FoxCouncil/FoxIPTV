// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Avalonia.Controls;
    using Classes;
    using LibVLCSharp.Shared;

    /// <summary>Everything the player does and everything LibVLC tells it, written to the log, and a watchdog for when playback goes wrong</summary>
    /// <remarks>
    /// Commands: every command the app sends LibVLC is logged when queued, started and finished, with timings; one still running after two seconds is reported while it runs.
    /// Events: every event LibVLC raises is logged from its own thread, from the event arguments alone, without asking LibVLC anything back.
    /// Clock: LibVLC keeps raising time updates while stalled, wobbling around one point, so an update arriving proves nothing. The watchdog tracks the furthest time reached and reports when it has not moved forward for three seconds, with the lowest and highest times reported meanwhile, and again when it moves.
    /// Pieces: every piece LibVLC fetches is sorted into media, caption or playlist; no media piece for fifteen seconds, or no playlist for thirty, is reported while playing, and again when they return.
    /// Heartbeat: every thirty seconds of playback, the piece ages and LibVLC's own counters (bytes read, pictures shown and lost, audio buffers lost), read off the UI thread.
    /// UI: the 100ms timer falling behind by 750ms or more is reported, since that means the window was not responding.
    /// </remarks>
    public partial class MainWindow
    {
        /// <summary>How long the playback time may stand still while playing before the watchdog speaks</summary>
        private static readonly TimeSpan ClockStallThreshold = TimeSpan.FromSeconds(3);

        /// <summary>How long no media piece may be fetched while playing before the watchdog speaks; pieces here run 2 to 10 seconds</summary>
        private static readonly TimeSpan MediaGapThreshold = TimeSpan.FromSeconds(15);

        /// <summary>How long no playlist may be fetched while playing a live stream before the watchdog speaks; live playlists refresh every few seconds</summary>
        private static readonly TimeSpan PlaylistGapThreshold = TimeSpan.FromSeconds(30);

        /// <summary>How long a command may run before it is reported as still running</summary>
        private static readonly TimeSpan SlowCommandThreshold = TimeSpan.FromSeconds(2);

        /// <summary>How late the 100ms timer may tick before the UI thread is reported as behind</summary>
        private static readonly TimeSpan UiLateThreshold = TimeSpan.FromMilliseconds(750);

        /// <summary>How far the playback time must move forward to count as moving</summary>
        private const long ClockProgressStepMs = 250;

        /// <summary>Numbers commands in the log</summary>
        private int _vlcCommandId;

        /// <summary>The command now running on the command thread, for the watchdog; null when idle</summary>
        private volatile string _vlcCommandRunning;

        /// <summary>When the running command started, Stopwatch ticks</summary>
        private long _vlcCommandStartedAt;

        /// <summary>The last time a running command was reported as slow, so it is reported every five seconds rather than every tick</summary>
        private long _vlcCommandReportedAt;

        /// <summary>The playback time LibVLC last reported, milliseconds; -1 before the first report of this stream</summary>
        private long _lastClockMs = -1;

        /// <summary>When LibVLC last reported the playback time, Stopwatch ticks; 0 before the first report</summary>
        private long _lastClockAt;

        /// <summary>The furthest playback time reached, milliseconds; -1 before the first report</summary>
        private long _clockProgressMs = -1;

        /// <summary>When the playback time last moved forward by <see cref="ClockProgressStepMs"/>, Stopwatch ticks; 0 before the first report</summary>
        private long _clockProgressAt;

        /// <summary>How many time updates arrived since the clock last moved forward</summary>
        private long _clockUpdatesSinceProgress;

        /// <summary>The lowest and highest times reported since the clock last moved forward</summary>
        private long _clockLowSinceProgress = long.MaxValue;

        private long _clockHighSinceProgress = long.MinValue;

        /// <summary>When the watchdog reported the clock standing still, Stopwatch ticks; 0 while it moves</summary>
        private long _stallReportedAt;

        /// <summary>When LibVLC last fetched a media piece (video or audio), a caption piece and a playlist, Stopwatch ticks; 0 for never in this stream</summary>
        private long _lastMediaPieceAt;

        private long _lastCaptionPieceAt;

        private long _lastPlaylistAt;

        /// <summary>When the watchdog reported no media pieces and no playlists, Stopwatch ticks; 0 while they arrive</summary>
        private long _mediaGapReportedAt;

        private long _playlistGapReportedAt;

        /// <summary>The last media piece fetched, for reports</summary>
        private volatile string _lastMediaPiece;

        /// <summary>When a buffering run began, Stopwatch ticks; 0 when not buffering</summary>
        private long _bufferingSince;

        /// <summary>The last buffering percentage, for reports</summary>
        private volatile int _lastBufferPercent = -1;

        /// <summary>True between LibVLC's Paused and its next Playing, when a still clock is expected</summary>
        private volatile bool _vlcPaused;

        /// <summary>When the 100ms timer last ticked, Stopwatch ticks</summary>
        private long _lastTickAt;

        /// <summary>1 while a heartbeat is reading LibVLC's counters, so two never overlap</summary>
        private int _statsPending;

        /// <summary>Stopwatch ticks now</summary>
        private static long Now => Stopwatch.GetTimestamp();

        /// <summary>Seconds since a Stopwatch tick count, or "never" when it is 0</summary>
        private static string Ago(long at)
        {
            return at == 0 ? "never" : $"{Stopwatch.GetElapsedTime(at).TotalSeconds:0.0}s ago";
        }

        /// <summary>A playback time for the log</summary>
        private static string Time(long ms)
        {
            return ms < 0 || ms == long.MaxValue || ms == long.MinValue ? "none" : TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss\.fff");
        }

        /// <summary>Wrap a command for the queue so it logs itself</summary>
        private Action WrapCommand(MediaPlayer player, Action<MediaPlayer> command, string name)
        {
            var id = Interlocked.Increment(ref _vlcCommandId);
            var queuedAt = Now;

            TvCore.LogInfo($"[VLC cmd #{id}] queued {name}, {_vlcCommands.Count} ahead of it");

            return () =>
            {
                TvCore.LogInfo($"[VLC cmd #{id}] start {name}, waited {Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:0}ms");

                _vlcCommandRunning = $"#{id} {name}";
                Interlocked.Exchange(ref _vlcCommandStartedAt, Now);
                Interlocked.Exchange(ref _vlcCommandReportedAt, 0);

                var clock = Stopwatch.StartNew();

                try
                {
                    command(player);

                    var took = clock.Elapsed;

                    if (took >= SlowCommandThreshold)
                    {
                        TvCore.LogError($"[VLC cmd #{id}] done {name} but slow, took {took.TotalMilliseconds:0}ms");
                    }
                    else
                    {
                        TvCore.LogInfo($"[VLC cmd #{id}] done {name} in {took.TotalMilliseconds:0}ms");
                    }
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[VLC cmd #{id}] failed {name} after {clock.Elapsed.TotalMilliseconds:0}ms: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    _vlcCommandRunning = null;
                }
            };
        }

        /// <summary>Log every event LibVLC raises; handlers only read their event arguments, never LibVLC itself</summary>
        private void HookDiagnostics()
        {
            var player = _player;

            player.Opening += (sender, args) => TvCore.LogInfo("[VLC event] Opening");

            player.Playing += (sender, args) =>
            {
                _vlcPaused = false;

                TvCore.LogInfo("[VLC event] Playing");
            };

            player.Paused += (sender, args) =>
            {
                _vlcPaused = true;

                TvCore.LogInfo("[VLC event] Paused");
            };

            player.Stopped += (sender, args) => TvCore.LogInfo("[VLC event] Stopped");
            player.EndReached += (sender, args) => TvCore.LogInfo("[VLC event] EndReached");
            player.EncounteredError += (sender, args) => TvCore.LogError("[VLC event] EncounteredError");
            player.Forward += (sender, args) => TvCore.LogInfo("[VLC event] Forward");
            player.Backward += (sender, args) => TvCore.LogInfo("[VLC event] Backward");
            player.Corked += (sender, args) => TvCore.LogInfo("[VLC event] Corked (audio output taken by another program)");
            player.Uncorked += (sender, args) => TvCore.LogInfo("[VLC event] Uncorked");
            player.Muted += (sender, args) => TvCore.LogInfo("[VLC event] Muted");
            player.Unmuted += (sender, args) => TvCore.LogInfo("[VLC event] Unmuted");
            player.VolumeChanged += (sender, args) => TvCore.LogInfo($"[VLC event] VolumeChanged {args.Volume:0.00}");
            player.MediaChanged += (sender, args) => TvCore.LogInfo("[VLC event] MediaChanged");
            player.LengthChanged += (sender, args) => TvCore.LogInfo($"[VLC event] LengthChanged {args.Length}ms");
            player.SeekableChanged += (sender, args) => TvCore.LogInfo($"[VLC event] SeekableChanged {args.Seekable}");
            player.PausableChanged += (sender, args) => TvCore.LogInfo($"[VLC event] PausableChanged {args.Pausable}");
            player.ScrambledChanged += (sender, args) => TvCore.LogInfo($"[VLC event] ScrambledChanged {args.Scrambled}");
            player.Vout += (sender, args) => TvCore.LogInfo($"[VLC event] Vout, {args.Count} video output(s)");
            player.ESAdded += (sender, args) => TvCore.LogInfo($"[VLC event] ESAdded {args.Type} id {args.Id}");
            player.ESDeleted += (sender, args) => TvCore.LogInfo($"[VLC event] ESDeleted {args.Type} id {args.Id}");
            player.ESSelected += (sender, args) => TvCore.LogInfo($"[VLC event] ESSelected {args.Type} id {args.Id}");

            player.Buffering += (sender, args) =>
            {
                var percent = (int)args.Cache;

                _lastBufferPercent = percent;

                if (percent < 100 && Interlocked.CompareExchange(ref _bufferingSince, Now, 0) == 0)
                {
                    TvCore.LogInfo($"[VLC event] Buffering started at {percent}%");
                }
                else if (percent >= 100)
                {
                    var since = Interlocked.Exchange(ref _bufferingSince, 0);

                    TvCore.LogInfo(since == 0 ? "[VLC event] Buffering 100%" : $"[VLC event] Buffering done after {Stopwatch.GetElapsedTime(since).TotalMilliseconds:0}ms");
                }
            };

            // Any exception on the UI thread ends up here before anything else sees it
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (sender, args) => TvCore.LogError($"[UI] Unhandled: {args.Exception.GetType().Name}: {args.Exception.Message}\n{args.Exception.StackTrace}");
        }

        /// <summary>LibVLC reported the playback time; called from LibVLC's thread</summary>
        private void NoteClock(long timeMs)
        {
            var now = Now;
            var previous = Interlocked.Exchange(ref _lastClockMs, timeMs);

            Interlocked.Exchange(ref _lastClockAt, now);

            if (previous > timeMs + 1000)
            {
                TvCore.LogInfo($"[Clock] went back from {Time(previous)} to {Time(timeMs)}");

                // A reset starts the count again from where the clock now is
                MarkProgress(timeMs, now);

                return;
            }

            var progress = Interlocked.Read(ref _clockProgressMs);

            if (progress >= 0 && timeMs < progress + ClockProgressStepMs)
            {
                // An update that did not move the clock on
                Interlocked.Increment(ref _clockUpdatesSinceProgress);

                InterlockedMin(ref _clockLowSinceProgress, timeMs);
                InterlockedMax(ref _clockHighSinceProgress, timeMs);

                return;
            }

            MarkProgress(timeMs, now);

            if (progress >= 0 && timeMs / 30000 != progress / 30000)
            {
                Heartbeat(timeMs);
            }
        }

        /// <summary>The clock moved forward: remember where to, and log the end of a reported stall</summary>
        private void MarkProgress(long timeMs, long now)
        {
            var stalledSince = Interlocked.Exchange(ref _stallReportedAt, 0);

            if (stalledSince != 0)
            {
                TvCore.LogError($"[Watchdog] Clock moving again at {Time(timeMs)}; it had not moved on from {Time(Interlocked.Read(ref _clockProgressMs))} for {Stopwatch.GetElapsedTime(Interlocked.Read(ref _clockProgressAt), now).TotalSeconds:0.0}s, " +
                                $"{Interlocked.Read(ref _clockUpdatesSinceProgress)} time updates meanwhile between {Time(Interlocked.Read(ref _clockLowSinceProgress))} and {Time(Interlocked.Read(ref _clockHighSinceProgress))}; last media piece {Ago(Interlocked.Read(ref _lastMediaPieceAt))}");
            }

            Interlocked.Exchange(ref _clockProgressMs, timeMs);
            Interlocked.Exchange(ref _clockProgressAt, now);
            Interlocked.Exchange(ref _clockUpdatesSinceProgress, 0);
            Interlocked.Exchange(ref _clockLowSinceProgress, long.MaxValue);
            Interlocked.Exchange(ref _clockHighSinceProgress, long.MinValue);
        }

        private static void InterlockedMin(ref long target, long value)
        {
            long current;

            while (value < (current = Interlocked.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }

        private static void InterlockedMax(ref long target, long value)
        {
            long current;

            while (value > (current = Interlocked.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }

        /// <summary>Every 30 seconds of playback: piece ages, and LibVLC's own counters read on the pool, never on the UI thread or inside LibVLC's callback</summary>
        private void Heartbeat(long timeMs)
        {
            var player = _player;

            if (player == null || Interlocked.Exchange(ref _statsPending, 1) == 1)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    var counters = "counters unavailable";

                    using (var media = player.Media)
                    {
                        var stats = media?.Statistics;

                        if (stats.HasValue)
                        {
                            var s = stats.Value;

                            counters = $"read {s.ReadBytes / 1048576.0:0.0}MB at {s.InputBitrate * 8000:0}kbps, demuxed {s.DemuxReadBytes / 1048576.0:0.0}MB, {s.DemuxCorrupted} corrupt, {s.DemuxDiscontinuity} discontinuities, " +
                                       $"video {s.DecodedVideo} decoded {s.DisplayedPictures} shown {s.LostPictures} lost, audio {s.DecodedAudio} decoded {s.PlayedAudioBuffers} played {s.LostAudioBuffers} lost";
                        }
                    }

                    TvCore.LogInfo($"[Clock] {Time(timeMs)}, last media piece {Ago(Interlocked.Read(ref _lastMediaPieceAt))}, last caption piece {Ago(Interlocked.Read(ref _lastCaptionPieceAt))}, last playlist {Ago(Interlocked.Read(ref _lastPlaylistAt))}, buffer {_lastBufferPercent}%; {counters}");
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[Clock] Heartbeat failed: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref _statsPending, 0);
                }
            });
        }

        /// <summary>Forget the clock and piece times, a new stream is starting</summary>
        private void ResetDiagnostics()
        {
            Interlocked.Exchange(ref _lastClockMs, -1);
            Interlocked.Exchange(ref _lastClockAt, 0);
            Interlocked.Exchange(ref _clockProgressMs, -1);
            Interlocked.Exchange(ref _clockProgressAt, 0);
            Interlocked.Exchange(ref _clockUpdatesSinceProgress, 0);
            Interlocked.Exchange(ref _clockLowSinceProgress, long.MaxValue);
            Interlocked.Exchange(ref _clockHighSinceProgress, long.MinValue);
            Interlocked.Exchange(ref _stallReportedAt, 0);
            Interlocked.Exchange(ref _lastMediaPieceAt, 0);
            Interlocked.Exchange(ref _lastCaptionPieceAt, 0);
            Interlocked.Exchange(ref _lastPlaylistAt, 0);
            Interlocked.Exchange(ref _mediaGapReportedAt, 0);
            Interlocked.Exchange(ref _playlistGapReportedAt, 0);
            Interlocked.Exchange(ref _bufferingSince, 0);

            _lastMediaPiece = null;
            _lastBufferPercent = -1;
            _vlcPaused = false;
        }

        /// <summary>Note what kind of piece a LibVLC "Retrieving" line fetched, and log pieces returning after a reported gap</summary>
        private void NotePiece(string message)
        {
            if (!message.StartsWith("Retrieving http", StringComparison.Ordinal))
            {
                return;
            }

            var url = message.Substring("Retrieving ".Length).Trim();
            var path = url.Split('?')[0];
            var now = Now;

            if (path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
            {
                var previous = Interlocked.Exchange(ref _lastPlaylistAt, now);

                if (Interlocked.Exchange(ref _playlistGapReportedAt, 0) != 0)
                {
                    TvCore.LogError($"[Watchdog] Playlists fetched again after {(previous == 0 ? 0 : Stopwatch.GetElapsedTime(previous, now).TotalSeconds):0.0}s without one");
                }
            }
            else if (path.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".webvtt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Exchange(ref _lastCaptionPieceAt, now);
            }
            else
            {
                var previous = Interlocked.Exchange(ref _lastMediaPieceAt, now);

                _lastMediaPiece = path.Length > 140 ? path.Substring(path.Length - 140) : path;

                if (Interlocked.Exchange(ref _mediaGapReportedAt, 0) != 0)
                {
                    TvCore.LogError($"[Watchdog] Media pieces fetched again after {(previous == 0 ? 0 : Stopwatch.GetElapsedTime(previous, now).TotalSeconds):0.0}s without one: {_lastMediaPiece}");
                }
            }
        }

        /// <summary>The 100ms check: the UI thread keeping up, commands finishing, pieces arriving, the clock moving while playing</summary>
        private void Watchdog()
        {
            var now = Now;
            var lastTick = Interlocked.Exchange(ref _lastTickAt, now);

            if (lastTick != 0)
            {
                var gap = Stopwatch.GetElapsedTime(lastTick, now);

                if (gap >= UiLateThreshold)
                {
                    TvCore.LogError($"[Watchdog] UI thread was busy for {gap.TotalMilliseconds:0}ms (timer due every 100ms)");
                }
            }

            var running = _vlcCommandRunning;
            var startedAt = Interlocked.Read(ref _vlcCommandStartedAt);

            if (running != null && startedAt != 0 && Stopwatch.GetElapsedTime(startedAt, now) >= SlowCommandThreshold)
            {
                var reportedAt = Interlocked.Read(ref _vlcCommandReportedAt);

                if (reportedAt == 0 || Stopwatch.GetElapsedTime(reportedAt, now) >= TimeSpan.FromSeconds(5))
                {
                    Interlocked.Exchange(ref _vlcCommandReportedAt, now);

                    TvCore.LogError($"[Watchdog] LibVLC command {running} still running after {Stopwatch.GetElapsedTime(startedAt, now).TotalSeconds:0.0}s");
                }
            }

            if (!_isPlaying || _vlcPaused)
            {
                return;
            }

            // No media piece for a while is the first sign of the stall where captions keep coming and the picture stops
            var mediaAt = Interlocked.Read(ref _lastMediaPieceAt);

            if (mediaAt != 0 && Interlocked.Read(ref _mediaGapReportedAt) == 0 && Stopwatch.GetElapsedTime(mediaAt, now) >= MediaGapThreshold)
            {
                Interlocked.Exchange(ref _mediaGapReportedAt, now);

                TvCore.LogError($"[Watchdog] No media piece fetched for {Stopwatch.GetElapsedTime(mediaAt, now).TotalSeconds:0.0}s while LibVLC says playing. {State()}");
            }

            // Live playlists refresh every few seconds; on-demand media fetches its playlist once, so only live channels count
            var playlistAt = Interlocked.Read(ref _lastPlaylistAt);

            if (TvCore.CurrentMedia == null && playlistAt != 0 && Interlocked.Read(ref _playlistGapReportedAt) == 0 && Stopwatch.GetElapsedTime(playlistAt, now) >= PlaylistGapThreshold)
            {
                Interlocked.Exchange(ref _playlistGapReportedAt, now);

                TvCore.LogError($"[Watchdog] No playlist fetched for {Stopwatch.GetElapsedTime(playlistAt, now).TotalSeconds:0.0}s while LibVLC says playing. {State()}");
            }

            var progressAt = Interlocked.Read(ref _clockProgressAt);

            if (progressAt == 0 || Interlocked.Read(ref _stallReportedAt) != 0)
            {
                return;
            }

            var still = Stopwatch.GetElapsedTime(progressAt, now);

            if (still < ClockStallThreshold)
            {
                return;
            }

            Interlocked.Exchange(ref _stallReportedAt, now);

            TvCore.LogError($"[Watchdog] Clock has not moved on from {Time(Interlocked.Read(ref _clockProgressMs))} for {still.TotalSeconds:0.0}s while LibVLC says playing; " +
                            $"{Interlocked.Read(ref _clockUpdatesSinceProgress)} time updates meanwhile between {Time(Interlocked.Read(ref _clockLowSinceProgress))} and {Time(Interlocked.Read(ref _clockHighSinceProgress))}, last update {Ago(Interlocked.Read(ref _lastClockAt))}. {State()}");
        }

        /// <summary>What was going on, for a watchdog report</summary>
        private string State()
        {
            return $"Last media piece {Ago(Interlocked.Read(ref _lastMediaPieceAt))} ({_lastMediaPiece ?? "none"}), last caption piece {Ago(Interlocked.Read(ref _lastCaptionPieceAt))}, last playlist {Ago(Interlocked.Read(ref _lastPlaylistAt))}. " +
                   $"Buffer {(_lastBufferPercent < 0 ? "never reported" : _lastBufferPercent + "%")}, command {(_vlcCommandRunning ?? "none running")}, {_vlcCommands.Count} queued, " +
                   $"captions {(TvCore.Settings.CCEnabled ? "on" : "off")}{(_ccFromSubtitleStream ? " (own stream)" : string.Empty)}{(_ccHeldForAds ? " held for ads" : string.Empty)}, in ad {AdDetector.InAd}, window {(IsVisible ? "shown" : "hidden")}.";
        }

        /// <summary>Log every click on the right-click menu, submenus included</summary>
        private static void LogMenuClicks(ItemsControl menu, string path)
        {
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                var header = $"{path}{item.Header}";

                item.Click += (sender, args) => TvCore.LogInfo($"[UI] Menu: {header}");

                LogMenuClicks(item, header + " > ");
            }
        }
    }
}
