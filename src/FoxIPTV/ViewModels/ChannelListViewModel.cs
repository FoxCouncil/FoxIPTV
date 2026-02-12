namespace FoxIPTV.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FoxIPTV.Models;
using FoxIPTV.Services;

public partial class ChannelListViewModel : ViewModelBase
{
    private readonly IIptvService _iptvService;
    private readonly ISettingsService _settingsService;
    private List<ChannelItemViewModel> _allChannels = [];

    public event Action<ChannelItemViewModel>? ChannelSelected;

    [ObservableProperty]
    private ObservableCollection<ChannelItemViewModel> _filteredChannels = [];

    [ObservableProperty]
    private ObservableCollection<string> _categories = ["All"];

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private ObservableCollection<string> _countries = ["All"];

    [ObservableProperty]
    private string _selectedCountry = "All";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ChannelItemViewModel? _selectedChannel;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private int _totalCount;

    public ChannelListViewModel(IIptvService iptvService, ISettingsService settingsService)
    {
        _iptvService = iptvService;
        _settingsService = settingsService;
    }

    public async Task LoadChannelsAsync(CancellationToken ct = default)
    {
        var channels = await _iptvService.GetChannelsWithStreamsAsync(ct);
        var countries = await _iptvService.GetCountriesAsync(ct);
        var favorites = _settingsService.Current.FavoriteChannelIds;

        var countryLookup = countries.ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);

        _allChannels = channels.Select(c =>
        {
            countryLookup.TryGetValue(c.Country, out var country);
            return new ChannelItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Country = c.Country,
                CountryName = country?.Name ?? c.Country,
                CountryFlag = country?.Flag ?? string.Empty,
                StreamUrl = c.StreamUrl,
                Quality = c.Quality,
                UserAgent = c.UserAgent,
                Referrer = c.Referrer,
                Categories = c.Categories,
                IsFavorite = favorites.Contains(c.Id)
            };
        }).ToList();

        // Build category list
        var cats = channels
            .SelectMany(c => c.Categories)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        Categories = new ObservableCollection<string>(["All", .. cats]);

        // Build country dropdown with flags and names
        var countryList = _allChannels
            .Where(c => !string.IsNullOrWhiteSpace(c.Country))
            .Select(c => c.CountryDisplay)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        Countries = new ObservableCollection<string>(["All", .. countryList]);

        TotalCount = _allChannels.Count;
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
    partial void OnSelectedCountryChanged(string value) => ApplyFilters();
    partial void OnShowFavoritesOnlyChanged(bool value) => ApplyFilters();

    partial void OnSelectedChannelChanged(ChannelItemViewModel? value)
    {
        if (value is not null)
        {
            ChannelSelected?.Invoke(value);
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(ChannelItemViewModel channel)
    {
        channel.IsFavorite = !channel.IsFavorite;

        var favorites = _settingsService.Current.FavoriteChannelIds;
        if (channel.IsFavorite)
        {
            favorites.Add(channel.Id);
        }
        else
        {
            favorites.Remove(channel.Id);
        }

        await _settingsService.SaveAsync();

        if (ShowFavoritesOnly)
        {
            ApplyFilters();
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allChannels.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            filtered = filtered.Where(c =>
                c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.CountryName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Country.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedCategory != "All")
        {
            filtered = filtered.Where(c => c.Categories.Contains(SelectedCategory));
        }

        if (SelectedCountry != "All")
        {
            filtered = filtered.Where(c => c.CountryDisplay == SelectedCountry);
        }

        if (ShowFavoritesOnly)
        {
            filtered = filtered.Where(c => c.IsFavorite);
        }

        FilteredChannels = new ObservableCollection<ChannelItemViewModel>(filtered);
    }
}
