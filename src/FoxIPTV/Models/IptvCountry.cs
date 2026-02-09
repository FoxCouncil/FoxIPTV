namespace FoxIPTV.Models;

using System.Text.Json.Serialization;

public sealed class IptvCountry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("languages")]
    public List<string> Languages { get; set; } = [];

    [JsonPropertyName("flag")]
    public string Flag { get; set; } = string.Empty;
}
