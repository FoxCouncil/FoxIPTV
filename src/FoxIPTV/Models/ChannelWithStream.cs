namespace FoxIPTV.Models;

public sealed class ChannelWithStream
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Country { get; init; }
    public List<string> Categories { get; init; } = [];
    public required string StreamUrl { get; init; }
    public string? Quality { get; init; }
    public string? UserAgent { get; init; }
    public string? Referrer { get; init; }
    public bool IsNsfw { get; init; }
}
