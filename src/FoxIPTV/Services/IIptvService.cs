namespace FoxIPTV.Services;

using FoxIPTV.Models;

public interface IIptvService
{
    string Name { get; }
    Task<IReadOnlyList<IptvChannel>> GetChannelsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IptvStream>> GetStreamsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IptvCategory>> GetCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IptvCountry>> GetCountriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IptvLanguage>> GetLanguagesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ChannelWithStream>> GetChannelsWithStreamsAsync(CancellationToken ct = default);
}
