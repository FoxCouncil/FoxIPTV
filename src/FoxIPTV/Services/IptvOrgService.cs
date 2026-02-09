namespace FoxIPTV.Services;

using System.Text.Json;
using FoxIPTV.Models;
using Microsoft.Extensions.Logging;

public sealed class IptvOrgService : IIptvService
{
    private const string BaseUrl = "https://iptv-org.github.io/api";

    private readonly HttpClient _httpClient;
    private readonly ICacheService _cache;
    private readonly ILogger<IptvOrgService> _logger;

    public string Name => "iptv-org (Free)";

    public IptvOrgService(HttpClient httpClient, ICacheService cache, ILogger<IptvOrgService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IptvChannel>> GetChannelsAsync(CancellationToken ct = default)
    {
        return await FetchAsync("channels.json", IptvJsonContext.Default.ListIptvChannel, TimeSpan.FromHours(12), ct) ?? [];
    }

    public async Task<IReadOnlyList<IptvStream>> GetStreamsAsync(CancellationToken ct = default)
    {
        return await FetchAsync("streams.json", IptvJsonContext.Default.ListIptvStream, TimeSpan.FromHours(6), ct) ?? [];
    }

    public async Task<IReadOnlyList<IptvCategory>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await FetchAsync("categories.json", IptvJsonContext.Default.ListIptvCategory, TimeSpan.FromHours(24), ct) ?? [];
    }

    public async Task<IReadOnlyList<IptvCountry>> GetCountriesAsync(CancellationToken ct = default)
    {
        return await FetchAsync("countries.json", IptvJsonContext.Default.ListIptvCountry, TimeSpan.FromHours(24), ct) ?? [];
    }

    public async Task<IReadOnlyList<IptvLanguage>> GetLanguagesAsync(CancellationToken ct = default)
    {
        return await FetchAsync("languages.json", IptvJsonContext.Default.ListIptvLanguage, TimeSpan.FromHours(24), ct) ?? [];
    }

    public async Task<IReadOnlyList<ChannelWithStream>> GetChannelsWithStreamsAsync(CancellationToken ct = default)
    {
        var channelsTask = GetChannelsAsync(ct);
        var streamsTask = GetStreamsAsync(ct);
        await Task.WhenAll(channelsTask, streamsTask);

        var channels = channelsTask.Result;
        var streams = streamsTask.Result;

        var streamLookup = streams.Where(s => s.Channel is not null).GroupBy(s => s.Channel!).ToDictionary(g => g.Key, g => g.First());

        _logger.LogInformation("Joining {ChannelCount} channels with {StreamCount} streams", channels.Count, streams.Count);

        return channels
            .Where(c => !c.IsNsfw && streamLookup.ContainsKey(c.Id))
            .Select(c =>
            {
                var stream = streamLookup[c.Id];
                return new ChannelWithStream
                {
                    Id = c.Id,
                    Name = c.Name,
                    Country = c.Country,
                    Categories = c.Categories,
                    StreamUrl = stream.Url,
                    Quality = stream.Quality,
                    UserAgent = stream.UserAgent,
                    Referrer = stream.Referrer,
                    IsNsfw = c.IsNsfw
                };
            })
            .OrderBy(c => c.Name)
            .ToList();
    }

    private async Task<T?> FetchAsync<T>(string resource, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, TimeSpan cacheDuration, CancellationToken ct) where T : class
    {
        var cached = await _cache.GetAsync<T>(resource, ct);
        if (cached is not null)
        {
            return cached;
        }

        var url = $"{BaseUrl}/{resource}";
        _logger.LogInformation("Fetching {Url}", url);

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync(stream, typeInfo, ct);

        if (result is not null)
        {
            await _cache.SetAsync(resource, result, cacheDuration, ct);
        }

        return result;
    }
}
