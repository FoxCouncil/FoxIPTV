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

    /// <summary>Everything the player does and everything LibVLC tells it, written to the log, and a watchdog for when playback goes quiet</summary>
    /// <remarks>
    /// Every command the app sends LibVLC is logged when it is queued, when it starts and when it ends, with how long each took; a command still running after two seconds is reported while it runs.
    /// Every event LibVLC raises is logged, from its own thread and without asking LibVLC anything back.
    /// The watchdog runs on the 100ms timer: if the playback clock stops moving while LibVLC says it is playing, it logs how long since the last video piece, caption piece and playlist were fetched and what the app was doing, then logs again when the clock moves.
    /// It also reports the UI thread falling behind, since a late timer means the window was not responding.
    /// </remarks>
    public partial class MainWindow
    {
        /// <summary>How long the clock may stand still while playing before the watchdog speaks</summary>
        private static readonly TimeSpan ClockStallThreshold = TimeSpan.FromSeconds(3);

        /// <summary>How long a command may run before it is reported as still running</summary>
        private static readonly TimeSpan SlowCommandThreshold = TimeSpan.FromSeconds(2);

        /// <summary>How late the 100ms timer may tick before the UI thread is reported as behind</summary>
        private static readonly TimeSpan UiLateThreshold = TimeSpan.FromMilliseconds(750);

        /// <summary>Numbers commands in the log</summary>
        private int _vlcCommandId;

        /// <summary>The command now running on the command thread, for the watchdog; null when idle</summary>
        private volatile string _vlcCommandRunning;

        /// <summary>When the running command started, Stopwatch ticks</summary>
        private long _vlcCommandStartedAt;

        /// <summary>The last time a running command was reported as slow, so it is reported every few seconds rather than every tick</summary>
        private long _vlcCommandReportedAt;

        /// <summary>The playback time LibVLC last reported, milliseconds</summary>
        private long _lastClockMs = -1;

        /// <summary>When LibVLC last reported the playback time, Stopwatch ticks; 0 before the first report of this stream</summary>
        private long _lastClockAt;

        /// <summary>When the watchdog reported the clock standing still, Stopwatch ticks; 0 while it moves</summary>
        private long _stallReportedAt;

        /// <summary>When LibVLC last fetched a media piece (video or audio), a caption piece and a playlist, Stopwatch ticks</summary>
        private long _lastMediaPieceAt;

        private long _lastCaptionPieceAt;

        private long _lastPlaylistAt;

        /// <summary>The last media piece fetched, for the stall report</summary>
        private volatile string _lastMediaPiece;

        /// <summary>When a buffering run began, Stopwatch ticks; 0 when not buffering</summary>
        private long _bufferingSince;

        /// <summary>The last buffering percentage, for the stall report</summary>
        private volatile int _lastBufferPercent = -1;

        /// <summary>True between LibVLC's Paused and its next Playing, when a still clock is expected</summary>
        private volatile bool _vlcPaused;

        /// <summary>When the 100ms timer last ticked, Stopwatch ticks</summary>
        private long _lastTickAt;

        /// <summary>Stopwatch ticks now</summary>
        private static long Now => Stopwatch.GetTimestamp();

        /// <summary>Seconds since a Stopwatch tick count, or "never" when it is 0</summary>
        private static string Ago(long at)
        {
            return at == 0 ? "never" : $"{Stopwatch.GetElapsedTime(at).TotalSeconds:0.0}s ago";
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

        /// <summary>LibVLC reported the playback time; logs the clock moving again after a stall, and a heartbeat every 30 seconds of playback</summary>
        private void NoteClock(long timeMs)
        {
            var previous = Interlocked.Exchange(ref _lastClockMs, timeMs);

            Interlocked.Exchange(ref _lastClockAt, Now);

            var stalledSince = Interlocked.Exchange(ref _stallReportedAt, 0);

            if (stalledSince != 0)
            {
                TvCore.LogError($"[Watchdog] Clock moving again at {TimeSpan.FromMilliseconds(timeMs):hh\\:mm\\:ss\\.fff}, it had stood still for {Stopwatch.GetElapsedTime(stalledSince).TotalSeconds + ClockStallThreshold.TotalSeconds:0.0}s; last media piece {Ago(Interlocked.Read(ref _lastMediaPieceAt))}");
            }

            if (previous >= 0 && timeMs / 30000 != previous / 30000)
            {
                TvCore.LogInfo($"[Clock] {TimeSpan.FromMilliseconds(timeMs):hh\\:mm\\:ss}, last media piece {Ago(Interlocked.Read(ref _lastMediaPieceAt))}, last caption piece {Ago(Interlocked.Read(ref _lastCaptionPieceAt))}");
            }

            if (previous > timeMs + 1000)
            {
                TvCore.LogInfo($"[Clock] went back from {previous}ms to {timeMs}ms");
            }
        }

        /// <summary>Forget the clock and piece times, a new stream is starting</summary>
        private void ResetDiagnostics()
        {
            Interlocked.Exchange(ref _lastClockMs, -1);
            Interlocked.Exchange(ref _lastClockAt, 0);
            Interlocked.Exchange(ref _stallReportedAt, 0);
            Interlocked.Exchange(ref _lastMediaPieceAt, 0);
            Interlocked.Exchange(ref _lastCaptionPieceAt, 0);
            Interlocked.Exchange(ref _lastPlaylistAt, 0);
            Interlocked.Exchange(ref _bufferingSince, 0);

            _lastMediaPiece = null;
            _lastBufferPercent = -1;
        }

        /// <summary>Note what kind of piece a LibVLC "Retrieving" line fetched</summary>
        private void NotePiece(string message)
        {
            if (!message.StartsWith("Retrieving http", StringComparison.Ordinal))
            {
                return;
            }

            var url = message.Substring("Retrieving ".Length).Trim();
            var path = url.Split('?')[0];

            if (path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Exchange(ref _lastPlaylistAt, Now);
            }
            else if (path.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".webvtt", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Exchange(ref _lastCaptionPieceAt, Now);
            }
            else
            {
                Interlocked.Exchange(ref _lastMediaPieceAt, Now);

                _lastMediaPiece = path.Length > 140 ? path.Substring(path.Length - 140) : path;
            }
        }

        /// <summary>The 100ms check: the UI thread keeping up, the clock moving while playing, commands finishing</summary>
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

            var clockAt = Interlocked.Read(ref _lastClockAt);

            if (!_isPlaying || _vlcPaused || clockAt == 0 || Interlocked.Read(ref _stallReportedAt) != 0)
            {
                return;
            }

            var still = Stopwatch.GetElapsedTime(clockAt, now);

            if (still < ClockStallThreshold)
            {
                return;
            }

            Interlocked.Exchange(ref _stallReportedAt, now);

            TvCore.LogError($"[Watchdog] Clock has stood still for {still.TotalSeconds:0.0}s at {TimeSpan.FromMilliseconds(Math.Max(0, Interlocked.Read(ref _lastClockMs))):hh\\:mm\\:ss\\.fff} while LibVLC says playing. " +
                            $"Last media piece {Ago(Interlocked.Read(ref _lastMediaPieceAt))} ({_lastMediaPiece ?? "none"}), last caption piece {Ago(Interlocked.Read(ref _lastCaptionPieceAt))}, last playlist {Ago(Interlocked.Read(ref _lastPlaylistAt))}. " +
                            $"Buffer {(_lastBufferPercent < 0 ? "never reported" : _lastBufferPercent + "%")}, command {(_vlcCommandRunning ?? "none running")}, {_vlcCommands.Count} queued, " +
                            $"captions {(TvCore.Settings.CCEnabled ? "on" : "off")}{(_ccFromSubtitleStream ? " (own stream)" : string.Empty)}{(_ccHeldForAds ? " held for ads" : string.Empty)}, in ad {AdDetector.InAd}, window {(IsVisible ? "shown" : "hidden")}.");
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
