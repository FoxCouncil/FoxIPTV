namespace FoxIPTV.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public partial class ChannelItemViewModel : ViewModelBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Country { get; init; }
    public required string StreamUrl { get; init; }
    public string? Quality { get; init; }
    public string? UserAgent { get; init; }
    public string? Referrer { get; init; }
    public List<string> Categories { get; init; } = [];

    [ObservableProperty]
    private bool _isFavorite;

    public string CategoryDisplay => Categories.Count > 0 ? string.Join(", ", Categories) : Country;
}
