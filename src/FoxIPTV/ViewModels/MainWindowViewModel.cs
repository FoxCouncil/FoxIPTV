namespace FoxIPTV.ViewModels;

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
    private bool _isToolBarVisible = true;

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
        _videoPlayer.PropertyChanged += OnVideoPlayerPropertyChanged;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing FoxIPTV...");
            await _settingsService.LoadAsync();
            await ChannelList.LoadChannelsAsync();
            IsLoading = false;
            StatusMessage = $"{ChannelList.TotalCount} channels  |  v{VersionString}";
            _logger.LogInformation("Loaded {Count} channels", ChannelList.TotalCount);

            // Auto-show sidebar after loading
            IsChannelListVisible = true;
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

        IsToolBarVisible = !value;
        FullScreenRequested?.Invoke(value);
    }

    [RelayCommand]
    private void ToggleChannelList()
    {
        IsChannelListVisible = !IsChannelListVisible;
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
        UpdateStatusMessage();
    }

    private void OnVideoPlayerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VideoPlayerViewModel.StreamInfo)
                           or nameof(VideoPlayerViewModel.IsPlaying)
                           or nameof(VideoPlayerViewModel.IsPaused)
                           or nameof(VideoPlayerViewModel.IsBuffering))
        {
            UpdateStatusMessage();
        }
    }

    private void UpdateStatusMessage()
    {
        if (!VideoPlayer.IsPlaying)
        {
            StatusMessage = $"{ChannelList.TotalCount} channels  |  v{VersionString}";
            return;
        }

        var state = VideoPlayer.IsBuffering ? "Buffering" :
                    VideoPlayer.IsPaused ? "Paused" : "Playing";

        var info = VideoPlayer.StreamInfo;
        StatusMessage = string.IsNullOrEmpty(info)
            ? $"{state}: {VideoPlayer.CurrentChannelName}"
            : $"{state}: {VideoPlayer.CurrentChannelName}  //  {info}";
    }
}
