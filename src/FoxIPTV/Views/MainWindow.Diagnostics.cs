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

    public partial class MainWindow
    {
        private static readonly TimeSpan ClockStallThreshold = TimeSpan.FromSeconds(3);

        private static readonly TimeSpan MediaGapThreshold = TimeSpan.FromSeconds(15);

        private static readonly TimeSpan PlaylistGapThreshold = TimeSpan.FromSeconds(30);

        private static readonly TimeSpan SlowCommandThreshold = TimeSpan.FromSeconds(2);

        private static readonly TimeSpan UiLateThreshold = TimeSpan.FromMilliseconds(750);

        private const long ClockProgressStepMs = 250;

        private int _vlcCommandId;

        private volatile string _vlcCommandRunning;

        private long _vlcCommandStartedAt;

        private long _vlcCommandReportedAt;

        private long _lastClockMs = -1;

        private long _lastClockAt;

        private long _clockProgressMs = -1;

        private long _clockProgressAt;

        private long _clockUpdatesSinceProgress;

        private long _clockLowSinceProgress = long.MaxValue;

        private long _clockHighSinceProgress = long.MinValue;

        private long _stallReportedAt;

        private long _lastMediaPieceAt;

        private long _lastCaptionPieceAt;

        private long _lastPlaylistAt;

        private long _mediaGapReportedAt;

        private long _playlistGapReportedAt;

        private volatile string _lastMediaPiece;

        private long _bufferingSince;

        private volatile int _lastBufferPercent = -1;

        private volatile bool _vlcPaused;

        private long _lastTickAt;

        private int _statsPending;

        private static long Now => Stopwatch.GetTimestamp();

        private static string Ago(long at)
        {
            return at == 0 ? "never" : $"{Stopwatch.GetElapsedTime(at).TotalSeconds:0.0}s ago";
        }

        private static string Time(long ms)
        {
            return ms < 0 || ms == long.MaxValue || ms == long.MinValue ? "none" : TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss\.fff");
        }

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
            player.ESAdded += (sender, args) => TvCore.LogInfo($"[VLC event]{(args.Type == TrackType.Text ? " [CC]" : string.Empty)} ESAdded {args.Type} id {args.Id}");
            player.ESDeleted += (sender, args) => TvCore.LogInfo($"[VLC event]{(args.Type == TrackType.Text ? " [CC]" : string.Empty)} ESDeleted {args.Type} id {args.Id}");
            player.ESSelected += (sender, args) => TvCore.LogInfo($"[VLC event]{(args.Type == TrackType.Text ? " [CC]" : string.Empty)} ESSelected {args.Type} id {args.Id}");

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

            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (sender, args) => TvCore.LogError($"[UI] Unhandled: {args.Exception.GetType().Name}: {args.Exception.Message}\n{args.Exception.StackTrace}");
        }

        private void NoteClock(long timeMs)
        {
            var now = Now;
            var previous = Interlocked.Exchange(ref _lastClockMs, timeMs);

            Interlocked.Exchange(ref _lastClockAt, now);

            if (previous > timeMs + 1000)
            {
                TvCore.LogInfo($"[Clock] went back from {Time(previous)} to {Time(timeMs)}");

                MarkProgress(timeMs, now);

                return;
            }

            var progress = Interlocked.Read(ref _clockProgressMs);

            if (progress >= 0 && timeMs < progress + ClockProgressStepMs)
            {
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
                    var spu = player.Spu;

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

                    TvCore.LogInfo($"[Clock] {Time(timeMs)}, last media piece {Ago(Interlocked.Read(ref _lastMediaPieceAt))}, last caption piece {Ago(Interlocked.Read(ref _lastCaptionPieceAt))}, last playlist {Ago(Interlocked.Read(ref _lastPlaylistAt))}, buffer {_lastBufferPercent}%, caption track {spu}, captions {(TvCore.Settings.CCEnabled ? "on" : "off")}{(_ccAvailable ? string.Empty : " (none usable)")}; {counters}");
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

            var mediaAt = Interlocked.Read(ref _lastMediaPieceAt);

            if (mediaAt != 0 && Interlocked.Read(ref _mediaGapReportedAt) == 0 && Stopwatch.GetElapsedTime(mediaAt, now) >= MediaGapThreshold)
            {
                Interlocked.Exchange(ref _mediaGapReportedAt, now);

                TvCore.LogError($"[Watchdog] No media piece fetched for {Stopwatch.GetElapsedTime(mediaAt, now).TotalSeconds:0.0}s while LibVLC says playing. {State()}");
            }

            var playlistAt = Interlocked.Read(ref _lastPlaylistAt);

            if (TvCore.CurrentMedia == null && playlistAt != 0 && Interlocked.Read(ref _playlistGapReportedAt) == 0 && Stopwatch.GetElapsedTime(playlistAt, now) >= PlaylistGapThreshold)
            {
                Interlocked.Exchange(ref _playlistGapReportedAt, now);

                TvCore.LogError($"[Watchdog] No playlist fetched for {Stopwatch.GetElapsedTime(playlistAt, now).TotalSeconds:0.0}s while LibVLC says playing. {State()}");
            }

            var progressAt = Interlocked.Read(ref _clockProgressAt);

            if (progressAt == 0 || Interlocked.Read(ref _stallReportedAt) != 0 || Interlocked.Read(ref _lastClockMs) == 0)
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

        private string State()
        {
            return $"Last media piece {Ago(Interlocked.Read(ref _lastMediaPieceAt))} ({_lastMediaPiece ?? "none"}), last caption piece {Ago(Interlocked.Read(ref _lastCaptionPieceAt))}, last playlist {Ago(Interlocked.Read(ref _lastPlaylistAt))}. " +
                   $"Buffer {(_lastBufferPercent < 0 ? "never reported" : _lastBufferPercent + "%")}, command {(_vlcCommandRunning ?? "none running")}, {_vlcCommands.Count} queued, " +
                   $"captions {(TvCore.Settings.CCEnabled ? "on" : "off")}{(_ccFromSubtitleStream ? " (own stream)" : string.Empty)}{(_ccHeldForAds ? " held for ads" : string.Empty)}, in ad {AdDetector.InAd}, window {(IsVisible ? "shown" : "hidden")}.";
        }

        private static readonly System.Text.RegularExpressions.Regex CaptionMessage = new System.Text.RegularExpressions.Regex(@"webvtt|\.vtt|spu|subtitle|subs|CC track|closed caption|cea.?608|cea.?708|c608|c708|cc|Restarting demuxer 1|deactivat|reactivat|sync reference|text track|teletext|dvbsub|tx3g|stpp|ttml", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private void NoteCaptionMessage(string message)
        {
            if (CaptionMessage.IsMatch(message))
            {
                TvCore.LogInfo($"[CC VLC] {message}");
            }
        }

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
