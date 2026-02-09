namespace FoxIPTV.Models;

public sealed class UserSettings
{
    public double Volume { get; set; } = 80;
    public bool IsMuted { get; set; }
    public string? LastChannelId { get; set; }
    public List<string> FavoriteChannelIds { get; set; } = [];
    public string? PreferredCountry { get; set; }
    public string? PreferredLanguage { get; set; }
    public bool FilterNsfw { get; set; } = true;
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 720;
}
