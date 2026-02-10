namespace FoxIPTV.Tests.ViewModels;

using FoxIPTV.Models;
using FoxIPTV.Services;
using FoxIPTV.ViewModels;
using NSubstitute;

public class ChannelListViewModelTests
{
    private readonly IIptvService _iptvService = Substitute.For<IIptvService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ChannelListViewModel _vm;

    private static readonly List<ChannelWithStream> SampleChannels =
    [
        new() { Id = "bbc", Name = "BBC News", Country = "UK", StreamUrl = "http://bbc/stream", Categories = ["News"] },
        new() { Id = "cnn", Name = "CNN", Country = "US", StreamUrl = "http://cnn/stream", Categories = ["News", "Entertainment"] },
        new() { Id = "espn", Name = "ESPN", Country = "US", StreamUrl = "http://espn/stream", Categories = ["Sports"] },
        new() { Id = "arte", Name = "Arte", Country = "FR", StreamUrl = "http://arte/stream", Categories = ["Entertainment"] },
        new() { Id = "nhk", Name = "NHK World", Country = "JP", StreamUrl = "http://nhk/stream", Categories = ["News"] },
    ];

    public ChannelListViewModelTests()
    {
        _settingsService.Current.Returns(new UserSettings());
        _iptvService.GetChannelsWithStreamsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ChannelWithStream>>(SampleChannels));
        _vm = new ChannelListViewModel(_iptvService, _settingsService);
    }

    [Fact]
    public async Task LoadChannels_PopulatesAllChannels()
    {
        await _vm.LoadChannelsAsync();

        Assert.Equal(5, _vm.TotalCount);
        Assert.Equal(5, _vm.FilteredChannels.Count);
    }

    [Fact]
    public async Task LoadChannels_BuildsCategoryList()
    {
        await _vm.LoadChannelsAsync();

        Assert.Contains("All", _vm.Categories);
        Assert.Contains("News", _vm.Categories);
        Assert.Contains("Sports", _vm.Categories);
        Assert.Contains("Entertainment", _vm.Categories);
        Assert.Equal(4, _vm.Categories.Count); // All + 3 unique categories
    }

    [Fact]
    public async Task LoadChannels_BuildsCountryList()
    {
        await _vm.LoadChannelsAsync();

        Assert.Contains("All", _vm.Countries);
        Assert.Contains("UK", _vm.Countries);
        Assert.Contains("US", _vm.Countries);
        Assert.Contains("FR", _vm.Countries);
        Assert.Contains("JP", _vm.Countries);
    }

    [Fact]
    public async Task LoadChannels_MarksFavorites()
    {
        _settingsService.Current.Returns(new UserSettings { FavoriteChannelIds = ["bbc", "espn"] });

        await _vm.LoadChannelsAsync();

        var bbc = _vm.FilteredChannels.First(c => c.Id == "bbc");
        var cnn = _vm.FilteredChannels.First(c => c.Id == "cnn");
        var espn = _vm.FilteredChannels.First(c => c.Id == "espn");

        Assert.True(bbc.IsFavorite);
        Assert.False(cnn.IsFavorite);
        Assert.True(espn.IsFavorite);
    }

    [Fact]
    public async Task SearchText_FiltersChannelsByName()
    {
        await _vm.LoadChannelsAsync();

        _vm.SearchText = "BBC";

        Assert.Single(_vm.FilteredChannels);
        Assert.Equal("bbc", _vm.FilteredChannels[0].Id);
    }

    [Fact]
    public async Task SearchText_FiltersChannelsByCountry()
    {
        await _vm.LoadChannelsAsync();

        _vm.SearchText = "US";

        Assert.Equal(2, _vm.FilteredChannels.Count);
        Assert.All(_vm.FilteredChannels, c => Assert.Equal("US", c.Country));
    }

    [Fact]
    public async Task SearchText_IsCaseInsensitive()
    {
        await _vm.LoadChannelsAsync();

        _vm.SearchText = "bbc";

        Assert.Single(_vm.FilteredChannels);
        Assert.Equal("bbc", _vm.FilteredChannels[0].Id);
    }

    [Fact]
    public async Task SelectedCategory_FiltersChannels()
    {
        await _vm.LoadChannelsAsync();

        _vm.SelectedCategory = "Sports";

        Assert.Single(_vm.FilteredChannels);
        Assert.Equal("espn", _vm.FilteredChannels[0].Id);
    }

    [Fact]
    public async Task SelectedCategory_All_ShowsAllChannels()
    {
        await _vm.LoadChannelsAsync();
        _vm.SelectedCategory = "Sports";
        Assert.Single(_vm.FilteredChannels);

        _vm.SelectedCategory = "All";

        Assert.Equal(5, _vm.FilteredChannels.Count);
    }

    [Fact]
    public async Task SelectedCountry_FiltersChannels()
    {
        await _vm.LoadChannelsAsync();

        _vm.SelectedCountry = "US";

        Assert.Equal(2, _vm.FilteredChannels.Count);
        Assert.All(_vm.FilteredChannels, c => Assert.Equal("US", c.Country));
    }

    [Fact]
    public async Task ShowFavoritesOnly_FiltersToFavorites()
    {
        _settingsService.Current.Returns(new UserSettings { FavoriteChannelIds = ["bbc", "espn"] });
        await _vm.LoadChannelsAsync();

        _vm.ShowFavoritesOnly = true;

        Assert.Equal(2, _vm.FilteredChannels.Count);
        Assert.All(_vm.FilteredChannels, c => Assert.True(c.IsFavorite));
    }

    [Fact]
    public async Task CombinedFilters_SearchAndCategory()
    {
        await _vm.LoadChannelsAsync();

        _vm.SelectedCategory = "News";
        _vm.SearchText = "BBC";

        Assert.Single(_vm.FilteredChannels);
        Assert.Equal("bbc", _vm.FilteredChannels[0].Id);
    }

    [Fact]
    public async Task CombinedFilters_CategoryAndCountry()
    {
        await _vm.LoadChannelsAsync();

        _vm.SelectedCategory = "News";
        _vm.SelectedCountry = "US";

        Assert.Single(_vm.FilteredChannels);
        Assert.Equal("cnn", _vm.FilteredChannels[0].Id);
    }

    [Fact]
    public async Task SelectedChannel_RaisesChannelSelectedEvent()
    {
        await _vm.LoadChannelsAsync();

        ChannelItemViewModel? selected = null;
        _vm.ChannelSelected += ch => selected = ch;

        _vm.SelectedChannel = _vm.FilteredChannels[0];

        Assert.NotNull(selected);
        Assert.Equal(_vm.FilteredChannels[0].Id, selected.Id);
    }

    [Fact]
    public async Task SelectedChannel_Null_DoesNotRaiseEvent()
    {
        await _vm.LoadChannelsAsync();

        bool eventRaised = false;
        _vm.ChannelSelected += _ => eventRaised = true;

        _vm.SelectedChannel = null;

        Assert.False(eventRaised);
    }

    [Fact]
    public async Task ToggleFavorite_AddsToFavorites()
    {
        var settings = new UserSettings();
        _settingsService.Current.Returns(settings);
        await _vm.LoadChannelsAsync();

        var channel = _vm.FilteredChannels.First(c => c.Id == "bbc");
        Assert.False(channel.IsFavorite);

        await _vm.ToggleFavoriteCommand.ExecuteAsync(channel);

        Assert.True(channel.IsFavorite);
        Assert.Contains("bbc", settings.FavoriteChannelIds);
        await _settingsService.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleFavorite_RemovesFromFavorites()
    {
        var settings = new UserSettings { FavoriteChannelIds = ["bbc"] };
        _settingsService.Current.Returns(settings);
        await _vm.LoadChannelsAsync();

        var channel = _vm.FilteredChannels.First(c => c.Id == "bbc");
        Assert.True(channel.IsFavorite);

        await _vm.ToggleFavoriteCommand.ExecuteAsync(channel);

        Assert.False(channel.IsFavorite);
        Assert.DoesNotContain("bbc", settings.FavoriteChannelIds);
    }

    [Fact]
    public async Task ToggleFavorite_WhenShowingFavoritesOnly_ReappliesFilter()
    {
        var settings = new UserSettings { FavoriteChannelIds = ["bbc", "cnn"] };
        _settingsService.Current.Returns(settings);
        await _vm.LoadChannelsAsync();
        _vm.ShowFavoritesOnly = true;
        Assert.Equal(2, _vm.FilteredChannels.Count);

        var bbc = _vm.FilteredChannels.First(c => c.Id == "bbc");
        await _vm.ToggleFavoriteCommand.ExecuteAsync(bbc);

        Assert.Single(_vm.FilteredChannels);
        Assert.Equal("cnn", _vm.FilteredChannels[0].Id);
    }

    [Fact]
    public async Task EmptySearchText_ShowsAllChannels()
    {
        await _vm.LoadChannelsAsync();
        _vm.SearchText = "BBC";
        Assert.Single(_vm.FilteredChannels);

        _vm.SearchText = "";

        Assert.Equal(5, _vm.FilteredChannels.Count);
    }

    [Fact]
    public async Task WhitespaceSearchText_ShowsAllChannels()
    {
        await _vm.LoadChannelsAsync();

        _vm.SearchText = "   ";

        Assert.Equal(5, _vm.FilteredChannels.Count);
    }
}
