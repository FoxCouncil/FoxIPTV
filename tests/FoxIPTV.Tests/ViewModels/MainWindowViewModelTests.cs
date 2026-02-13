namespace FoxIPTV.Tests.ViewModels;

using FoxIPTV.Models;
using FoxIPTV.Services;
using FoxIPTV.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;

public class MainWindowViewModelTests
{
    private readonly ChannelListViewModel _channelList;
    private readonly VideoPlayerViewModel _videoPlayer = new();
    private readonly ISettingsService _settingsService;
    private readonly MainWindowViewModel _vm;

    private static readonly List<ChannelWithStream> SampleChannels =
    [
        new() { Id = "bbc", Name = "BBC News", Country = "UK", StreamUrl = "http://bbc/stream", Categories = ["News"] },
        new() { Id = "cnn", Name = "CNN", Country = "US", StreamUrl = "http://cnn/stream", Categories = ["News"] },
        new() { Id = "espn", Name = "ESPN", Country = "US", StreamUrl = "http://espn/stream", Categories = ["Sports"] },
    ];

    public MainWindowViewModelTests()
    {
        var iptvService = Substitute.For<IIptvService>();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new UserSettings());
        iptvService.GetChannelsWithStreamsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ChannelWithStream>>(SampleChannels));

        _channelList = new ChannelListViewModel(iptvService, _settingsService);
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();
        _vm = new MainWindowViewModel(_channelList, _videoPlayer, _settingsService, logger);
    }

    [Fact]
    public void InitialState_IsLoadingTrue()
    {
        Assert.True(_vm.IsLoading);
        Assert.Equal("Loading channels...", _vm.StatusMessage);
    }

    [Fact]
    public void ToggleFullScreen_FlipsState()
    {
        Assert.False(_vm.IsFullScreen);

        _vm.ToggleFullScreenCommand.Execute(null);
        Assert.True(_vm.IsFullScreen);

        _vm.ToggleFullScreenCommand.Execute(null);
        Assert.False(_vm.IsFullScreen);
    }

    [Fact]
    public void FullScreen_HidesSidebar()
    {
        _vm.IsChannelListVisible = true;

        _vm.IsFullScreen = true;

        Assert.False(_vm.IsChannelListVisible);
    }

    [Fact]
    public void ExitFullScreen_RestoresSidebar()
    {
        _vm.IsChannelListVisible = true;
        _vm.IsFullScreen = true;
        Assert.False(_vm.IsChannelListVisible);

        _vm.IsFullScreen = false;

        Assert.True(_vm.IsChannelListVisible);
    }

    [Fact]
    public void FullScreen_RaisesFullScreenRequestedEvent()
    {
        bool? receivedValue = null;
        _vm.FullScreenRequested += val => receivedValue = val;

        _vm.IsFullScreen = true;

        Assert.True(receivedValue);
    }

    [Fact]
    public void ToggleChannelList_FlipsVisibility()
    {
        Assert.False(_vm.IsChannelListVisible);

        _vm.ToggleChannelListCommand.Execute(null);
        Assert.True(_vm.IsChannelListVisible);

        _vm.ToggleChannelListCommand.Execute(null);
        Assert.False(_vm.IsChannelListVisible);
    }

    [Fact]
    public void TitleText_ContainsVersion()
    {
        Assert.StartsWith("FoxIPTV v", _vm.TitleText);
    }

    [Fact]
    public void VideoPlayerProperty_IsAccessible()
    {
        Assert.Same(_videoPlayer, _vm.VideoPlayer);
    }

    [Fact]
    public void ChannelListProperty_IsAccessible()
    {
        Assert.Same(_channelList, _vm.ChannelList);
    }

    // --- Volume restore ---

    [Fact]
    public async Task Initialize_RestoresVolumeFromSettings()
    {
        _settingsService.Current.Returns(new UserSettings { Volume = 42 });

        await _vm.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(42, _vm.VideoPlayer.Volume);
    }

    [Fact]
    public async Task Initialize_RestoresMuteFromSettings()
    {
        _settingsService.Current.Returns(new UserSettings { IsMuted = true });

        await _vm.InitializeCommand.ExecuteAsync(null);

        Assert.True(_vm.VideoPlayer.IsMuted);
    }

    [Fact]
    public async Task VolumeChange_AfterInit_SavesSettings()
    {
        _settingsService.Current.Returns(new UserSettings());
        await _vm.InitializeCommand.ExecuteAsync(null);
        _settingsService.ClearReceivedCalls();

        _vm.VideoPlayer.Volume = 55;

        Assert.Equal(55, _settingsService.Current.Volume);
        await _settingsService.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MuteChange_AfterInit_SavesSettings()
    {
        _settingsService.Current.Returns(new UserSettings());
        await _vm.InitializeCommand.ExecuteAsync(null);
        _settingsService.ClearReceivedCalls();

        _vm.VideoPlayer.IsMuted = true;

        Assert.True(_settingsService.Current.IsMuted);
        await _settingsService.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VolumeChange_BeforeInit_DoesNotSave()
    {
        _settingsService.Current.Returns(new UserSettings());

        _vm.VideoPlayer.Volume = 55;

        await _settingsService.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    // --- Sidebar restore ---

    [Fact]
    public async Task Initialize_RestoresSidebarVisible()
    {
        _settingsService.Current.Returns(new UserSettings { IsChannelListVisible = true });

        await _vm.InitializeCommand.ExecuteAsync(null);

        Assert.True(_vm.IsChannelListVisible);
    }

    [Fact]
    public async Task Initialize_RestoresSidebarHidden()
    {
        _settingsService.Current.Returns(new UserSettings { IsChannelListVisible = false });

        await _vm.InitializeCommand.ExecuteAsync(null);

        Assert.False(_vm.IsChannelListVisible);
    }

    [Fact]
    public async Task ToggleChannelList_AfterInit_SavesSettings()
    {
        _settingsService.Current.Returns(new UserSettings { IsChannelListVisible = false });
        await _vm.InitializeCommand.ExecuteAsync(null);
        _settingsService.ClearReceivedCalls();

        _vm.ToggleChannelListCommand.Execute(null);

        Assert.True(_settingsService.Current.IsChannelListVisible);
        await _settingsService.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FullScreen_DoesNotSaveSidebarState()
    {
        _settingsService.Current.Returns(new UserSettings { IsChannelListVisible = true });
        await _vm.InitializeCommand.ExecuteAsync(null);
        _settingsService.ClearReceivedCalls();

        // Entering fullscreen hides sidebar but should NOT save
        _vm.IsFullScreen = true;

        await _settingsService.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    // --- Last channel / auto-resume ---

    [Fact]
    public async Task Initialize_WithLastChannelId_AutoResumes()
    {
        _settingsService.Current.Returns(new UserSettings { LastChannelId = "cnn" });

        string? playedUrl = null;
        _vm.VideoPlayer.PlayRequested += (url, _, _) => playedUrl = url;

        await _vm.InitializeCommand.ExecuteAsync(null);

        Assert.True(_vm.VideoPlayer.IsPlaying);
        Assert.Equal("CNN", _vm.VideoPlayer.CurrentChannelName);
        Assert.Equal("http://cnn/stream", playedUrl);
    }

    [Fact]
    public async Task Initialize_WithUnknownLastChannelId_DoesNotCrash()
    {
        _settingsService.Current.Returns(new UserSettings { LastChannelId = "nonexistent" });

        await _vm.InitializeCommand.ExecuteAsync(null);

        Assert.False(_vm.VideoPlayer.IsPlaying);
    }

    [Fact]
    public async Task Initialize_WithNoLastChannelId_DoesNotAutoPlay()
    {
        _settingsService.Current.Returns(new UserSettings { LastChannelId = null });

        await _vm.InitializeCommand.ExecuteAsync(null);

        Assert.False(_vm.VideoPlayer.IsPlaying);
    }

    [Fact]
    public async Task ChannelSelected_AfterInit_SavesLastChannelId()
    {
        _settingsService.Current.Returns(new UserSettings());
        await _vm.InitializeCommand.ExecuteAsync(null);
        _settingsService.ClearReceivedCalls();

        _channelList.SelectedChannel = _channelList.FilteredChannels.First(c => c.Id == "espn");

        Assert.Equal("espn", _settingsService.Current.LastChannelId);
        await _settingsService.Received().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChannelSelected_SameChannel_DoesNotRestart()
    {
        _settingsService.Current.Returns(new UserSettings());
        await _vm.InitializeCommand.ExecuteAsync(null);

        // Select a channel
        _channelList.SelectedChannel = _channelList.FilteredChannels.First(c => c.Id == "bbc");
        Assert.True(_vm.VideoPlayer.IsPlaying);

        // Track play requests
        int playCount = 0;
        _vm.VideoPlayer.PlayRequested += (_, _, _) => playCount++;

        // Re-select same channel — should be de-bounced
        _channelList.SelectedChannel = null; // reset to allow re-selection
        _channelList.SelectedChannel = _channelList.FilteredChannels.First(c => c.Id == "bbc");

        Assert.Equal(0, playCount);
    }

    // --- Favorites persistence (integration) ---

    [Fact]
    public async Task Favorites_SurviveReload()
    {
        // Simulate saved favorites from a previous session
        var settings = new UserSettings { FavoriteChannelIds = ["bbc", "espn"] };
        _settingsService.Current.Returns(settings);

        await _vm.InitializeCommand.ExecuteAsync(null);

        var bbc = _channelList.FilteredChannels.First(c => c.Id == "bbc");
        var cnn = _channelList.FilteredChannels.First(c => c.Id == "cnn");
        var espn = _channelList.FilteredChannels.First(c => c.Id == "espn");

        Assert.True(bbc.IsFavorite);
        Assert.False(cnn.IsFavorite);
        Assert.True(espn.IsFavorite);
    }

    [Fact]
    public async Task ToggleFavorite_SavesViaSettingsService()
    {
        var settings = new UserSettings();
        _settingsService.Current.Returns(settings);
        await _vm.InitializeCommand.ExecuteAsync(null);
        _settingsService.ClearReceivedCalls();

        var channel = _channelList.FilteredChannels.First(c => c.Id == "cnn");
        await _channelList.ToggleFavoriteCommand.ExecuteAsync(channel);

        Assert.True(channel.IsFavorite);
        Assert.Contains("cnn", settings.FavoriteChannelIds);
        await _settingsService.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    // --- Combined settings restore ---

    [Fact]
    public async Task Initialize_RestoresAllSettings()
    {
        _settingsService.Current.Returns(new UserSettings
        {
            Volume = 35,
            IsMuted = true,
            IsChannelListVisible = false,
            LastChannelId = "espn"
        });

        await _vm.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(35, _vm.VideoPlayer.Volume);
        Assert.True(_vm.VideoPlayer.IsMuted);
        Assert.False(_vm.IsChannelListVisible);
        Assert.True(_vm.VideoPlayer.IsPlaying);
        Assert.Equal("ESPN", _vm.VideoPlayer.CurrentChannelName);
    }
}
