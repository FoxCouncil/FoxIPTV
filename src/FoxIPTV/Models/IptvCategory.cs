namespace FoxIPTV.Models;

using System.Text.Json.Serialization;

public sealed class IptvCategory
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
