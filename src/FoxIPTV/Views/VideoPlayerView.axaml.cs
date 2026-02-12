using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using FoxIPTV.Services;
using FoxIPTV.ViewModels;
using LibVLCSharp.Shared;

namespace FoxIPTV.Views;

public partial class VideoPlayerView : UserControl
{
    [DllImport("libc", SetLastError = true)]
    private static extern int setenv(string name, string value, int overwrite);

    private static bool _nativeResolverRegistered;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private string _lastStreamInfo = string.Empty;

    public VideoPlayerView()
    {
        InitializeComponent();

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        try
        {
            RegisterNativeResolver();

            var args = GetLibVlcArgs();
            _libVlc = new LibVLC(args);
            _mediaPlayer = new MediaPlayer(_libVlc);

            _mediaPlayer.EncounteredError += OnPlayerError;
            _mediaPlayer.Buffering += OnPlayerBuffering;
            _mediaPlayer.Playing += OnPlayerPlaying;
            _mediaPlayer.Paused += OnPlayerPaused;

            VideoView.MediaPlayer = _mediaPlayer;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to initialize LibVLC: {ex.Message}. " + GetPlatformHelpMessage());
        }
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.EncounteredError -= OnPlayerError;
            _mediaPlayer.Buffering -= OnPlayerBuffering;
            _mediaPlayer.Playing -= OnPlayerPlaying;
            _mediaPlayer.Paused -= OnPlayerPaused;
            _mediaPlayer.Stop();
            _mediaPlayer.Dispose();
            _mediaPlayer = null;
        }

        _libVlc?.Dispose();
        _libVlc = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is VideoPlayerViewModel vm)
        {
            vm.PlayRequested += OnPlayRequested;
            vm.StopRequested += OnStopRequested;
            vm.VolumeChangeRequested += OnVolumeChangeRequested;
            vm.PauseRequested += OnPauseRequested;
            vm.ResumeRequested += OnResumeRequested;
            vm.StatsUpdateRequested += OnStatsUpdateRequested;
            vm.AudioTrackChangeRequested += OnAudioTrackChangeRequested;
            vm.SubtitleTrackChangeRequested += OnSubtitleTrackChangeRequested;
        }
    }

    private void OnPlayRequested(string streamUrl, string? userAgent, string? referrer)
    {
        if (_libVlc is null || _mediaPlayer is null)
        {
            ShowError("LibVLC is not initialized. " + GetPlatformHelpMessage());
            return;
        }

        try
        {
            var media = new Media(_libVlc, new Uri(streamUrl));

            if (!string.IsNullOrEmpty(userAgent))
            {
                media.AddOption($":http-user-agent={userAgent}");
            }

            if (!string.IsNullOrEmpty(referrer))
            {
                media.AddOption($":http-referrer={referrer}");
            }

            _mediaPlayer.Play(media);

            if (DataContext is VideoPlayerViewModel vm)
            {
                _mediaPlayer.Volume = vm.IsMuted ? 0 : (int)vm.Volume;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Playback error: {ex.Message}");
        }
    }

    private void OnStopRequested()
    {
        _lastStreamInfo = string.Empty;
        _mediaPlayer?.Stop();
    }

    private void OnPauseRequested()
    {
        _mediaPlayer?.SetPause(true);
    }

    private void OnResumeRequested()
    {
        _mediaPlayer?.SetPause(false);
    }

    private void OnVolumeChangeRequested(int volume)
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Volume = volume;
        }
    }

    private void OnAudioTrackChangeRequested(int trackId)
    {
        if (_mediaPlayer is null) return;
        _mediaPlayer.SetAudioTrack(trackId);
        RefreshTrackLists();
    }

    private void OnSubtitleTrackChangeRequested(int trackId)
    {
        if (_mediaPlayer is null) return;
        _mediaPlayer.SetSpu(trackId);
        RefreshTrackLists();
    }

    private void RefreshTrackLists()
    {
        if (_mediaPlayer is null || DataContext is not VideoPlayerViewModel vm) return;

        try
        {
            var currentAudioId = _mediaPlayer.AudioTrack;
            var audioDescs = _mediaPlayer.AudioTrackDescription;
            if (audioDescs.Length > 1)
            {
                vm.AudioTracks = audioDescs
                    .Select(d => new TrackOption(d.Id, d.Id == -1 ? "Off" : d.Name, d.Id == currentAudioId))
                    .ToList();
            }

            var currentSpuId = _mediaPlayer.Spu;
            var spuDescs = _mediaPlayer.SpuDescription;
            if (spuDescs.Length > 1)
            {
                vm.SubtitleTracks = spuDescs
                    .Select(d => new TrackOption(d.Id, d.Id == -1 ? "Off" : d.Name, d.Id == currentSpuId))
                    .ToList();
            }
        }
        catch { /* track descriptions not always available */ }
    }

    private void OnPlayerError(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ShowError("Playback failed. The stream may be offline or geo-restricted.");
        });
    }

    private void OnPlayerBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is VideoPlayerViewModel vm)
            {
                vm.OnBuffering(e.Cache);
            }
        });
    }

    private void OnPlayerPlaying(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is VideoPlayerViewModel vm)
            {
                vm.OnPlaying();
            }
        });
    }

    private void OnPlayerPaused(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is VideoPlayerViewModel vm)
            {
                vm.OnPaused();
            }
        });
    }

    private string OnStatsUpdateRequested()
    {
        if (_mediaPlayer is null)
            return _lastStreamInfo;

        // During adaptive bitrate reconnects, IsPlaying is briefly false.
        // Return cached info instead of blanking out.
        if (!_mediaPlayer.IsPlaying)
            return _lastStreamInfo;

        var parts = new List<string>();
        var resolution = string.Empty;
        var videoCodecStr = string.Empty;
        var audioCodecStr = string.Empty;
        var audioLayout = string.Empty;

        // Video resolution from actual decoded output (updates during adaptive bitrate switches)
        uint videoWidth = 0, videoHeight = 0;
        if (_mediaPlayer.Size(0, ref videoWidth, ref videoHeight) && videoWidth > 0)
        {
            parts.Add($"{videoWidth}x{videoHeight}");
            resolution = FormatResolution(videoHeight);

            // FPS from track metadata
            var video = _mediaPlayer.Media?.Tracks
                .Where(t => t.TrackType == TrackType.Video)
                .Select(t => t.Data.Video)
                .FirstOrDefault();

            if (video.HasValue && video.Value.FrameRateNum > 0 && video.Value.FrameRateDen > 0)
            {
                var fps = (double)video.Value.FrameRateNum / video.Value.FrameRateDen;
                parts.Add($"{fps:F1}fps");
            }
        }

        // Codec info
        if (_mediaPlayer.Media?.Tracks is { } tracks)
        {
            var vc = tracks.FirstOrDefault(t => t.TrackType == TrackType.Video);
            if (!string.IsNullOrEmpty(vc.Description))
            {
                videoCodecStr = vc.Description;
                parts.Add(vc.Description);
            }
            else if (vc.Codec != 0)
            {
                videoCodecStr = FourCcToString(vc.Codec);
                parts.Add(videoCodecStr);
            }

            var ac = tracks.FirstOrDefault(t => t.TrackType == TrackType.Audio);
            if (!string.IsNullOrEmpty(ac.Description))
            {
                audioCodecStr = ac.Description;
                parts.Add(ac.Description);
            }
            else if (ac.Codec != 0)
            {
                audioCodecStr = FourCcToString(ac.Codec);
                parts.Add(audioCodecStr);
            }
        }

        // Audio track info
        var audioTrackIdx = _mediaPlayer.AudioTrack;
        if (audioTrackIdx != -1)
        {
            var audio = _mediaPlayer.Media?.Tracks
                .Where(t => t.TrackType == TrackType.Audio)
                .Select(t => t.Data.Audio)
                .FirstOrDefault();

            if (audio.HasValue && audio.Value.Rate > 0)
            {
                parts.Add($"{audio.Value.Rate / 1000}kHz {audio.Value.Channels}ch");
                audioLayout = FormatAudioChannels(audio.Value.Channels);
            }
        }

        // Track counts (player-level, survives reconnects better than Media.Tracks)
        var audioTrackCount = 0;
        var subtitleTrackCount = 0;
        try
        {
            audioTrackCount = Math.Max(0, _mediaPlayer.AudioTrackCount - 1);
            subtitleTrackCount = Math.Max(0, _mediaPlayer.SpuCount - 1);
        }
        catch { /* not available for all media */ }

        // Media statistics: bitrate, data read, dropped frames
        try
        {
            var stats = _mediaPlayer.Media?.Statistics;
            if (stats.HasValue)
            {
                var s = stats.Value;
                if (s.InputBitrate > 0)
                {
                    var kbps = s.InputBitrate * 8;
                    parts.Add(kbps >= 1000 ? $"{kbps / 1000:F1} Mbps" : $"{kbps:F0} Kbps");
                }

                if (s.DemuxReadBytes > 0)
                    parts.Add($"{s.DemuxReadBytes / 1024.0 / 1024.0:F1} MB");

                if (s.LostPictures > 0)
                    parts.Add($"{s.LostPictures} dropped");
            }
        }
        catch
        {
            // Statistics may not be available for all media types
        }

        // Push track details to ViewModel for toolbar display.
        // Only update when we got valid data — during adaptive reconnects,
        // Tracks can be temporarily null, which would blank out the badges.
        if (DataContext is VideoPlayerViewModel vm)
        {
            if (!string.IsNullOrEmpty(resolution))
            {
                vm.VideoResolution = resolution;
                vm.VideoCodecName = videoCodecStr;
                vm.AudioCodecName = audioCodecStr;
                vm.AudioChannelLayout = audioLayout;
            }

            if (audioTrackCount > 0)
                vm.AudioTrackCount = audioTrackCount;
            if (subtitleTrackCount > 0)
                vm.SubtitleTrackCount = subtitleTrackCount;

            // Populate track lists for flyout menus
            try
            {
                var currentAudioId = _mediaPlayer.AudioTrack;
                var audioDescs = _mediaPlayer.AudioTrackDescription;
                if (audioDescs.Length > 1)
                {
                    vm.AudioTracks = audioDescs
                        .Select(d => new TrackOption(d.Id, d.Id == -1 ? "Off" : d.Name, d.Id == currentAudioId))
                        .ToList();
                }

                var currentSpuId = _mediaPlayer.Spu;
                var spuDescs = _mediaPlayer.SpuDescription;
                if (spuDescs.Length > 1) // >1 because first entry is always "Disable"
                {
                    vm.SubtitleTracks = spuDescs
                        .Select(d => new TrackOption(d.Id, d.Id == -1 ? "Off" : d.Name, d.Id == currentSpuId))
                        .ToList();
                }
            }
            catch { /* track descriptions not available for all media */ }
        }

        var result = parts.Count > 0 ? string.Join("  |  ", parts) : string.Empty;
        if (!string.IsNullOrEmpty(result))
            _lastStreamInfo = result;

        return _lastStreamInfo;
    }

    private static string FormatResolution(uint height)
    {
        return height switch
        {
            >= 2160 => "4K",
            >= 1440 => "1440p",
            >= 1080 => "1080p",
            >= 720 => "720p",
            >= 480 => "480p",
            >= 360 => "360p",
            _ => $"{height}p"
        };
    }

    private static string FormatAudioChannels(uint channels)
    {
        return channels switch
        {
            1 => "Mono",
            2 => "Stereo",
            6 => "5.1",
            8 => "7.1",
            _ => $"{channels}ch"
        };
    }

    public event EventHandler? VideoDoubleTapped;

    public void OnVideoPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is VideoPlayerViewModel vm)
        {
            vm.ShowOverlay();
        }
    }

    public void OnVideoDoubleTapped(object? sender, TappedEventArgs e)
    {
        VideoDoubleTapped?.Invoke(this, EventArgs.Empty);
    }

    private void ShowError(string message)
    {
        if (DataContext is VideoPlayerViewModel vm)
        {
            vm.ErrorMessage = message;
            vm.IsPlaying = false;
            vm.IsBuffering = false;
        }
    }

    /// <summary>
    /// Pre-loads libvlc native libraries from VLC.app on macOS and registers
    /// a DllImport resolver as fallback. Pre-loading is needed because
    /// Core.Initialize uses NativeLibrary.Load internally (not DllImport),
    /// so SetDllImportResolver alone is insufficient.
    /// </summary>
    private static void RegisterNativeResolver()
    {
        if (_nativeResolverRegistered) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string vlcLibPath;
            string vlcPluginPath;

            if (VlcNativeManager.LibPath is not null)
            {
                vlcLibPath = VlcNativeManager.LibPath;
                vlcPluginPath = VlcNativeManager.PluginPath!;
            }
            else
            {
                vlcLibPath = "/Applications/VLC.app/Contents/MacOS/lib/";
                vlcPluginPath = "/Applications/VLC.app/Contents/MacOS/plugins/";
            }

            setenv("DYLD_LIBRARY_PATH", vlcLibPath, 1);
            setenv("VLC_PLUGIN_PATH", vlcPluginPath, 1);

            NativeLibrary.Load(Path.Combine(vlcLibPath, "libvlccore.dylib"));
            NativeLibrary.Load(Path.Combine(vlcLibPath, "libvlc.dylib"));

            NativeLibrary.SetDllImportResolver(typeof(LibVLC).Assembly, (name, assembly, path) =>
            {
                if (name is "libvlc" or "libvlccore")
                {
                    if (NativeLibrary.TryLoad(Path.Combine(vlcLibPath, name + ".dylib"), out var handle))
                        return handle;
                }
                return IntPtr.Zero;
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && VlcNativeManager.LibPath is not null)
        {
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", VlcNativeManager.PluginPath);
            Core.Initialize(VlcNativeManager.LibPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && VlcNativeManager.LibPath is not null)
        {
            setenv("VLC_PLUGIN_PATH", VlcNativeManager.PluginPath!, 1);
            var libPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
            setenv("LD_LIBRARY_PATH", VlcNativeManager.LibPath + ":" + libPath, 1);
            Core.Initialize(VlcNativeManager.LibPath);
        }

        _nativeResolverRegistered = true;
    }

    private static string[] GetLibVlcArgs()
    {
        var args = new List<string>
        {
            "--no-video-title-show"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            args.Add("--no-xlib");

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            {
                args.Add("--vout=xcb_x11");
            }
        }

        return args.ToArray();
    }

    private static string FourCcToString(uint fourcc)
    {
        var chars = new char[4];
        chars[0] = (char)(fourcc & 0xFF);
        chars[1] = (char)((fourcc >> 8) & 0xFF);
        chars[2] = (char)((fourcc >> 16) & 0xFF);
        chars[3] = (char)((fourcc >> 24) & 0xFF);
        return new string(chars).Trim('\0').ToUpperInvariant();
    }

    private static string GetPlatformHelpMessage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "On Linux, ensure libvlc is installed: sudo apt install vlc libvlc-dev (Debian/Ubuntu) or sudo dnf install vlc vlc-devel (Fedora).";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "On macOS, install VLC from https://www.videolan.org or: brew install --cask vlc";
        }

        return "Ensure VLC or LibVLC is installed on your system.";
    }
}
