namespace FoxIPTV.Models;

using System.Text.Json.Serialization;

public sealed class IptvStream
{
    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("feed")]
    public string? Feed { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("referrer")]
    public string? Referrer { get; set; }
}
