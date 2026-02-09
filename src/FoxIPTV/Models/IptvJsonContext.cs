namespace FoxIPTV.Models;

using System.Text.Json.Serialization;

[JsonSerializable(typeof(List<IptvChannel>))]
[JsonSerializable(typeof(List<IptvStream>))]
[JsonSerializable(typeof(List<IptvCategory>))]
[JsonSerializable(typeof(List<IptvCountry>))]
[JsonSerializable(typeof(List<IptvLanguage>))]
[JsonSerializable(typeof(UserSettings))]
public partial class IptvJsonContext : JsonSerializerContext;
