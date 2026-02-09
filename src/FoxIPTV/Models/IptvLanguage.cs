namespace FoxIPTV.Models;

using System.Text.Json.Serialization;

public sealed class IptvLanguage
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
