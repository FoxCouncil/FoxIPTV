namespace FoxIPTV.ViewModels;

using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FoxIPTV.Services;
using Microsoft.Extensions.Logging;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private bool _initialized;

    [ObservableProperty]
    private ChannelListViewModel _channelList;

    [ObservableProperty]
    private VideoPlayerViewModel _videoPlayer;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _statusMessage = "Loading channels...";

    [ObservableProperty]
    private bool _isFullScreen;

    [ObservableProperty]
    private bool _isChannelListVisible;

    public string TitleText => $"FoxIPTV v{VersionString}";

    public static string VersionString { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?.Split('+')[0] ?? "0.0.0";

    public event Action<bool>? FullScreenRequested;

    public MainWindowViewModel(
        ChannelListViewModel channelList,
        VideoPlayerViewModel videoPlayer,
        ISettingsService settingsService,
        ILogger<MainWindowViewModel> logger)
    {
        _channelList = channelList;
        _videoPlayer = videoPlayer;
        _settingsService = settingsService;
        _logger = logger;

        _channelList.ChannelSelected += OnChannelSelected;
        _videoPlayer.ToggleFullScreenRequested += () => ToggleFullScreen();
        _videoPlayer.ToggleChannelListRequested += () => ToggleChannelList();
        _videoPlayer.PropertyChanged += OnVideoPlayerPropertyChanged;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing FoxIPTV...");
            await _settingsService.LoadAsync();

            var settings = _settingsService.Current;

            // Restore volume/mute before channels load (no media playing yet)
            VideoPlayer.Volume = settings.Volume;
            VideoPlayer.IsMuted = settings.IsMuted;

            await ChannelList.LoadChannelsAsync();
            IsLoading = false;
            StatusMessage = $"{ChannelList.TotalCount} channels  |  v{VersionString}";
            _logger.LogInformation("Loaded {Count} channels", ChannelList.TotalCount);

            // Restore sidebar visibility
            IsChannelListVisible = settings.IsChannelListVisible;

            // Auto-resume last channel
            if (settings.LastChannelId is { } lastId)
            {
                var channel = ChannelList.FilteredChannels.FirstOrDefault(c => c.Id == lastId);
                if (channel is not null)
                {
                    VideoPlayer.PlayChannel(channel);
                }
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load channels");
            IsLoading = false;
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private bool _sidebarWasVisible;

    [RelayCommand]
    private void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
    }

    partial void OnIsFullScreenChanged(bool value)
    {
        if (value)
        {
            _sidebarWasVisible = IsChannelListVisible;
            IsChannelListVisible = false;
        }
        else
        {
            IsChannelListVisible = _sidebarWasVisible;
        }

        FullScreenRequested?.Invoke(value);
    }

    [RelayCommand]
    private void ToggleChannelList()
    {
        IsChannelListVisible = !IsChannelListVisible;
        SaveSidebarState();
    }

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void OnChannelSelected(ChannelItemViewModel channel)
    {
        // De-bounce: don't restart if the same channel is already playing
        if (VideoPlayer.IsPlaying && VideoPlayer.CurrentStreamUrl == channel.StreamUrl)
            return;

        VideoPlayer.PlayChannel(channel);

        if (_initialized)
        {
            _settingsService.Current.LastChannelId = channel.Id;
            _ = _settingsService.SaveAsync();
        }
    }

    private void OnVideoPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_initialized) return;

        switch (e.PropertyName)
        {
            case nameof(VideoPlayerViewModel.Volume):
                _settingsService.Current.Volume = VideoPlayer.Volume;
                _ = _settingsService.SaveAsync();
                break;
            case nameof(VideoPlayerViewModel.IsMuted):
                _settingsService.Current.IsMuted = VideoPlayer.IsMuted;
                _ = _settingsService.SaveAsync();
                break;
        }
    }

    private void SaveSidebarState()
    {
        if (!_initialized) return;
        _settingsService.Current.IsChannelListVisible = IsChannelListVisible;
        _ = _settingsService.SaveAsync();
    }
}
