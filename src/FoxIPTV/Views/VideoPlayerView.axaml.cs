using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
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
        if (_mediaPlayer is null || !_mediaPlayer.IsPlaying)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        // Video track info
        var videoTrack = _mediaPlayer.VideoTrack;
        if (videoTrack != -1)
        {
            var width = _mediaPlayer.Media?.Tracks
                .Where(t => t.TrackType == TrackType.Video)
                .Select(t => t.Data.Video)
                .FirstOrDefault();

            if (width.HasValue && width.Value.Width > 0)
            {
                parts.Add($"{width.Value.Width}x{width.Value.Height}");

                if (width.Value.FrameRateNum > 0 && width.Value.FrameRateDen > 0)
                {
                    var fps = (double)width.Value.FrameRateNum / width.Value.FrameRateDen;
                    parts.Add($"{fps:F1}fps");
                }
            }
        }

        // Audio track info
        var audioTrack = _mediaPlayer.AudioTrack;
        if (audioTrack != -1)
        {
            var audio = _mediaPlayer.Media?.Tracks
                .Where(t => t.TrackType == TrackType.Audio)
                .Select(t => t.Data.Audio)
                .FirstOrDefault();

            if (audio.HasValue && audio.Value.Rate > 0)
            {
                parts.Add($"{audio.Value.Rate / 1000}kHz");
                parts.Add($"{audio.Value.Channels}ch");
            }
        }

        // Codec info from track descriptions
        if (_mediaPlayer.Media?.Tracks is { } tracks)
        {
            var videoCodec = tracks.FirstOrDefault(t => t.TrackType == TrackType.Video);
            if (!string.IsNullOrEmpty(videoCodec.Description))
            {
                parts.Add(videoCodec.Description);
            }
            else if (videoCodec.Codec != 0)
            {
                parts.Add(FourCcToString(videoCodec.Codec));
            }

            var audioCodec = tracks.FirstOrDefault(t => t.TrackType == TrackType.Audio);
            if (!string.IsNullOrEmpty(audioCodec.Description))
            {
                parts.Add(audioCodec.Description);
            }
            else if (audioCodec.Codec != 0)
            {
                parts.Add(FourCcToString(audioCodec.Codec));
            }
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : string.Empty;
    }

    public void OnVideoPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is VideoPlayerViewModel vm)
        {
            vm.ShowOverlay();
        }
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
        if (_nativeResolverRegistered)
        {
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            const string vlcBasePath = "/Applications/VLC.app/Contents/MacOS/";
            const string vlcLibPath = vlcBasePath + "lib/";

            // Use native setenv so libvlc's getenv() sees these values.
            // .NET's Environment.SetEnvironmentVariable may not propagate to native code.
            setenv("DYLD_LIBRARY_PATH", vlcLibPath, 1);
            setenv("VLC_PLUGIN_PATH", vlcBasePath + "plugins/", 1);

            // Pre-load native libraries so they're available globally.
            // libvlccore must be loaded before libvlc (dependency order).
            NativeLibrary.Load(vlcLibPath + "libvlccore.dylib");
            NativeLibrary.Load(vlcLibPath + "libvlc.dylib");

            // Also register DllImport resolver for any P/Invoke calls
            NativeLibrary.SetDllImportResolver(typeof(LibVLC).Assembly, (name, assembly, path) =>
            {
                if (name is "libvlc" or "libvlccore")
                {
                    if (NativeLibrary.TryLoad(vlcLibPath + name + ".dylib", out var handle))
                    {
                        return handle;
                    }
                }

                return IntPtr.Zero;
            });
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
