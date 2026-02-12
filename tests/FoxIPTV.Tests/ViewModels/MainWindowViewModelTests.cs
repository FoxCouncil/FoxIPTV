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
    private readonly MainWindowViewModel _vm;

    public MainWindowViewModelTests()
    {
        var iptvService = Substitute.For<IIptvService>();
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new UserSettings());
        iptvService.GetChannelsWithStreamsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ChannelWithStream>>([]));

        _channelList = new ChannelListViewModel(iptvService, settingsService);
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();
        _vm = new MainWindowViewModel(_channelList, _videoPlayer, settingsService, logger);
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
    public void FullScreen_HidesToolBar()
    {
        Assert.True(_vm.IsToolBarVisible);

        _vm.IsFullScreen = true;

        Assert.False(_vm.IsToolBarVisible);
    }

    [Fact]
    public void ExitFullScreen_ShowsToolBar()
    {
        _vm.IsFullScreen = true;
        Assert.False(_vm.IsToolBarVisible);

        _vm.IsFullScreen = false;

        Assert.True(_vm.IsToolBarVisible);
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
}
