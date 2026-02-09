namespace FoxIPTV.Models;

using System.Text.Json.Serialization;

public sealed class IptvChannel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("alt_names")]
    public List<string> AltNames { get; set; } = [];

    [JsonPropertyName("network")]
    public string? Network { get; set; }

    [JsonPropertyName("owners")]
    public List<string> Owners { get; set; } = [];

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];

    [JsonPropertyName("is_nsfw")]
    public bool IsNsfw { get; set; }

    [JsonPropertyName("launched")]
    public string? Launched { get; set; }

    [JsonPropertyName("closed")]
    public string? Closed { get; set; }

    [JsonPropertyName("replaced_by")]
    public string? ReplacedBy { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }
}
