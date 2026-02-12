namespace FoxIPTV.ViewModels;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class VideoPlayerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _currentStreamUrl;

    [ObservableProperty]
    private string _currentChannelName = string.Empty;

    [ObservableProperty]
    private string _currentCountryFlag = string.Empty;

    [ObservableProperty]
    private string _currentCountryName = string.Empty;

    [ObservableProperty]
    private double _volume = 80;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isBuffering;

    [ObservableProperty]
    private double _bufferProgress;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isOverlayVisible = true;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _streamInfo = string.Empty;

    [ObservableProperty]
    private string _videoResolution = string.Empty;

    [ObservableProperty]
    private string _videoCodecName = string.Empty;

    [ObservableProperty]
    private string _audioCodecName = string.Empty;

    [ObservableProperty]
    private string _audioChannelLayout = string.Empty;

    [ObservableProperty]
    private int _audioTrackCount;

    [ObservableProperty]
    private int _subtitleTrackCount;

    [ObservableProperty]
    private List<TrackOption> _audioTracks = [];

    [ObservableProperty]
    private List<TrackOption> _subtitleTracks = [];

    public bool HasMultipleAudioTracks => AudioTrackCount > 1;
    public bool HasSubtitles => SubtitleTrackCount > 0;

    private DispatcherTimer? _overlayTimer;
    private DispatcherTimer? _statsTimer;

    public bool ShouldShowOverlay => IsOverlayVisible || IsBuffering || IsPaused;

    public event Action<string, string?, string?>? PlayRequested;
    public event Action? StopRequested;
    public event Action<int>? VolumeChangeRequested;
    public event Action? PauseRequested;
    public event Action? ResumeRequested;
    public event Func<string>? StatsUpdateRequested;
    public event Action<int>? AudioTrackChangeRequested;
    public event Action<int>? SubtitleTrackChangeRequested;

    public void PlayChannel(ChannelItemViewModel channel)
    {
        ErrorMessage = null;
        CurrentStreamUrl = channel.StreamUrl;
        CurrentChannelName = channel.Name;
        CurrentCountryFlag = channel.CountryFlag;
        CurrentCountryName = channel.CountryName;
        IsBuffering = true;
        BufferProgress = 0;
        IsPaused = false;
        IsPlaying = true;

        // Clear stale track info from previous channel
        VideoResolution = string.Empty;
        VideoCodecName = string.Empty;
        AudioCodecName = string.Empty;
        AudioChannelLayout = string.Empty;
        AudioTrackCount = 0;
        SubtitleTrackCount = 0;
        AudioTracks = [];
        SubtitleTracks = [];

        PlayRequested?.Invoke(channel.StreamUrl, channel.UserAgent, channel.Referrer);
    }

    public void OnBuffering(float percentage)
    {
        BufferProgress = percentage;
        IsBuffering = percentage < 100;
    }

    public void OnPlaying()
    {
        IsBuffering = false;
        BufferProgress = 100;
        IsPaused = false;
        StartStatsTimer();
    }

    public void OnPaused()
    {
        IsPaused = true;
    }

    public void ShowOverlay()
    {
        IsOverlayVisible = true;
        _overlayTimer?.Stop();
        _overlayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _overlayTimer.Tick += (_, _) =>
        {
            _overlayTimer.Stop();
            if (IsPlaying && !IsBuffering && !IsPaused)
            {
                IsOverlayVisible = false;
            }
        };
        _overlayTimer.Start();
    }

    partial void OnIsOverlayVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShouldShowOverlay));
    }

    partial void OnIsBufferingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShouldShowOverlay));
    }

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShouldShowOverlay));
    }

    partial void OnVolumeChanged(double value)
    {
        VolumeChangeRequested?.Invoke((int)value);
    }

    partial void OnIsMutedChanged(bool value)
    {
        VolumeChangeRequested?.Invoke(value ? 0 : (int)Volume);
    }

    partial void OnAudioTrackCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasMultipleAudioTracks));
    }

    partial void OnSubtitleTrackCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasSubtitles));
    }

    [RelayCommand]
    private void SelectAudioTrack(int trackId)
    {
        AudioTrackChangeRequested?.Invoke(trackId);
    }

    [RelayCommand]
    private void SelectSubtitleTrack(int trackId)
    {
        SubtitleTrackChangeRequested?.Invoke(trackId);
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (IsPaused)
        {
            IsPaused = false;
            ResumeRequested?.Invoke();
        }
        else
        {
            IsPaused = true;
            PauseRequested?.Invoke();
        }
    }

    [RelayCommand]
    private void Stop()
    {
        StopStatsTimer();
        IsPlaying = false;
        IsBuffering = false;
        BufferProgress = 0;
        IsPaused = false;
        CurrentStreamUrl = null;
        CurrentChannelName = string.Empty;
        CurrentCountryFlag = string.Empty;
        CurrentCountryName = string.Empty;
        StreamInfo = string.Empty;
        VideoResolution = string.Empty;
        VideoCodecName = string.Empty;
        AudioCodecName = string.Empty;
        AudioChannelLayout = string.Empty;
        AudioTrackCount = 0;
        SubtitleTrackCount = 0;
        AudioTracks = [];
        SubtitleTracks = [];
        StopRequested?.Invoke();
    }

    private void StartStatsTimer()
    {
        _statsTimer?.Stop();
        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statsTimer.Tick += (_, _) =>
        {
            if (StatsUpdateRequested is not null)
            {
                StreamInfo = StatsUpdateRequested.Invoke();
            }
        };
        _statsTimer.Start();

        // Immediate first update
        if (StatsUpdateRequested is not null)
        {
            StreamInfo = StatsUpdateRequested.Invoke();
        }
    }

    private void StopStatsTimer()
    {
        _statsTimer?.Stop();
        _statsTimer = null;
    }
}
