namespace FoxIPTV.Tests.Services;

using System.Net;
using System.Text;
using System.Text.Json;
using FoxIPTV.Models;
using FoxIPTV.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

public class IptvOrgServiceTests
{
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ILogger<IptvOrgService> _logger = Substitute.For<ILogger<IptvOrgService>>();

    private IptvOrgService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://iptv-org.github.io/api/") };
        return new IptvOrgService(httpClient, _cache, _logger);
    }

    private static FakeHttpHandler CreateHandler(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new FakeHttpHandler(json, statusCode);
    }

    [Fact]
    public async Task GetChannelsAsync_ReturnsCachedData_WhenAvailable()
    {
        var cached = new List<IptvChannel>
        {
            new() { Id = "cached.uk", Name = "Cached", Country = "UK", Categories = [], AltNames = [], Owners = [] }
        };
        _cache.GetAsync<List<IptvChannel>>("channels.json", Arg.Any<CancellationToken>())
            .Returns(cached);

        var service = CreateService(CreateHandler("[]")); // HTTP shouldn't be called
        var result = await service.GetChannelsAsync();

        Assert.Single(result);
        Assert.Equal("cached.uk", result[0].Id);
    }

    [Fact]
    public async Task GetChannelsAsync_FetchesFromApi_WhenNotCached()
    {
        _cache.GetAsync<List<IptvChannel>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvChannel>?)null);

        var channels = new List<IptvChannel>
        {
            new() { Id = "bbc.uk", Name = "BBC", Country = "UK", Categories = ["News"], AltNames = [], Owners = [] }
        };
        var json = JsonSerializer.Serialize(channels);
        var service = CreateService(CreateHandler(json));

        var result = await service.GetChannelsAsync();

        Assert.Single(result);
        Assert.Equal("bbc.uk", result[0].Id);
    }

    [Fact]
    public async Task GetChannelsAsync_CachesResult_AfterFetch()
    {
        _cache.GetAsync<List<IptvChannel>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvChannel>?)null);

        var json = JsonSerializer.Serialize(new List<IptvChannel>
        {
            new() { Id = "test", Name = "Test", Country = "US", Categories = [], AltNames = [], Owners = [] }
        });
        var service = CreateService(CreateHandler(json));

        await service.GetChannelsAsync();

        await _cache.Received(1).SetAsync(
            "channels.json",
            Arg.Any<List<IptvChannel>>(),
            TimeSpan.FromHours(12),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetChannelsWithStreams_JoinsChannelsAndStreams()
    {
        _cache.GetAsync<List<IptvChannel>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvChannel>?)null);
        _cache.GetAsync<List<IptvStream>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvStream>?)null);

        var channels = new List<IptvChannel>
        {
            new() { Id = "bbc.uk", Name = "BBC News", Country = "UK", Categories = ["News"], IsNsfw = false, AltNames = [], Owners = [] },
            new() { Id = "cnn.us", Name = "CNN", Country = "US", Categories = ["News"], IsNsfw = false, AltNames = [], Owners = [] },
        };
        var streams = new List<IptvStream>
        {
            new() { Channel = "bbc.uk", Url = "http://bbc/stream.m3u8", Quality = "720p" },
            new() { Channel = "cnn.us", Url = "http://cnn/stream.m3u8" },
        };

        var handler = new MultiResponseHandler(new Dictionary<string, string>
        {
            ["channels.json"] = JsonSerializer.Serialize(channels),
            ["streams.json"] = JsonSerializer.Serialize(streams),
        });
        var service = CreateService(handler);

        var result = await service.GetChannelsWithStreamsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("BBC News", result[0].Name); // Ordered by name
        Assert.Equal("CNN", result[1].Name);
        Assert.Equal("http://bbc/stream.m3u8", result[0].StreamUrl);
    }

    [Fact]
    public async Task GetChannelsWithStreams_FiltersNsfw()
    {
        _cache.GetAsync<List<IptvChannel>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvChannel>?)null);
        _cache.GetAsync<List<IptvStream>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvStream>?)null);

        var channels = new List<IptvChannel>
        {
            new() { Id = "safe.uk", Name = "Safe Channel", Country = "UK", Categories = [], IsNsfw = false, AltNames = [], Owners = [] },
            new() { Id = "nsfw.uk", Name = "NSFW Channel", Country = "UK", Categories = [], IsNsfw = true, AltNames = [], Owners = [] },
        };
        var streams = new List<IptvStream>
        {
            new() { Channel = "safe.uk", Url = "http://safe/stream" },
            new() { Channel = "nsfw.uk", Url = "http://nsfw/stream" },
        };

        var handler = new MultiResponseHandler(new Dictionary<string, string>
        {
            ["channels.json"] = JsonSerializer.Serialize(channels),
            ["streams.json"] = JsonSerializer.Serialize(streams),
        });
        var service = CreateService(handler);

        var result = await service.GetChannelsWithStreamsAsync();

        Assert.Single(result);
        Assert.Equal("safe.uk", result[0].Id);
    }

    [Fact]
    public async Task GetChannelsWithStreams_ExcludesChannelsWithoutStreams()
    {
        _cache.GetAsync<List<IptvChannel>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvChannel>?)null);
        _cache.GetAsync<List<IptvStream>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvStream>?)null);

        var channels = new List<IptvChannel>
        {
            new() { Id = "has.stream", Name = "Has Stream", Country = "US", Categories = [], IsNsfw = false, AltNames = [], Owners = [] },
            new() { Id = "no.stream", Name = "No Stream", Country = "US", Categories = [], IsNsfw = false, AltNames = [], Owners = [] },
        };
        var streams = new List<IptvStream>
        {
            new() { Channel = "has.stream", Url = "http://test/stream" },
        };

        var handler = new MultiResponseHandler(new Dictionary<string, string>
        {
            ["channels.json"] = JsonSerializer.Serialize(channels),
            ["streams.json"] = JsonSerializer.Serialize(streams),
        });
        var service = CreateService(handler);

        var result = await service.GetChannelsWithStreamsAsync();

        Assert.Single(result);
        Assert.Equal("has.stream", result[0].Id);
    }

    [Fact]
    public async Task GetChannelsWithStreams_OrdersByName()
    {
        _cache.GetAsync<List<IptvChannel>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvChannel>?)null);
        _cache.GetAsync<List<IptvStream>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvStream>?)null);

        var channels = new List<IptvChannel>
        {
            new() { Id = "z", Name = "Zebra TV", Country = "US", Categories = [], IsNsfw = false, AltNames = [], Owners = [] },
            new() { Id = "a", Name = "Alpha TV", Country = "US", Categories = [], IsNsfw = false, AltNames = [], Owners = [] },
            new() { Id = "m", Name = "Middle TV", Country = "US", Categories = [], IsNsfw = false, AltNames = [], Owners = [] },
        };
        var streams = new List<IptvStream>
        {
            new() { Channel = "z", Url = "http://z" },
            new() { Channel = "a", Url = "http://a" },
            new() { Channel = "m", Url = "http://m" },
        };

        var handler = new MultiResponseHandler(new Dictionary<string, string>
        {
            ["channels.json"] = JsonSerializer.Serialize(channels),
            ["streams.json"] = JsonSerializer.Serialize(streams),
        });
        var service = CreateService(handler);

        var result = await service.GetChannelsWithStreamsAsync();

        Assert.Equal("Alpha TV", result[0].Name);
        Assert.Equal("Middle TV", result[1].Name);
        Assert.Equal("Zebra TV", result[2].Name);
    }

    [Fact]
    public async Task GetChannelsWithStreams_PreservesStreamMetadata()
    {
        _cache.GetAsync<List<IptvChannel>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvChannel>?)null);
        _cache.GetAsync<List<IptvStream>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvStream>?)null);

        var channels = new List<IptvChannel>
        {
            new() { Id = "ch1", Name = "Channel 1", Country = "US", Categories = ["News"], IsNsfw = false, AltNames = [], Owners = [] },
        };
        var streams = new List<IptvStream>
        {
            new() { Channel = "ch1", Url = "http://stream/1", Quality = "1080p", UserAgent = "MyAgent", Referrer = "http://ref" },
        };

        var handler = new MultiResponseHandler(new Dictionary<string, string>
        {
            ["channels.json"] = JsonSerializer.Serialize(channels),
            ["streams.json"] = JsonSerializer.Serialize(streams),
        });
        var service = CreateService(handler);

        var result = await service.GetChannelsWithStreamsAsync();

        Assert.Single(result);
        Assert.Equal("1080p", result[0].Quality);
        Assert.Equal("MyAgent", result[0].UserAgent);
        Assert.Equal("http://ref", result[0].Referrer);
        Assert.Contains("News", result[0].Categories);
    }

    [Fact]
    public async Task GetStreamsAsync_ReturnsEmptyOnNull()
    {
        _cache.GetAsync<List<IptvStream>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<IptvStream>?)null);

        // Simulate API returning null (empty JSON)
        var service = CreateService(CreateHandler("null"));
        var result = await service.GetStreamsAsync();

        Assert.Empty(result);
    }
}

internal class FakeHttpHandler(string json, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }
}

internal class MultiResponseHandler(Dictionary<string, string> responses) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        // Match on the last segment (e.g., "api/channels.json" → "channels.json")
        var resource = path.Split('/').Last();

        if (responses.TryGetValue(resource, out var json))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
