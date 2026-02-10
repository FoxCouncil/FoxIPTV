namespace FoxIPTV.Tests.Models;

using FoxIPTV.Models;

public class ChannelWithStreamTests
{
    [Fact]
    public void RequiredProperties_AreSet()
    {
        var channel = new ChannelWithStream
        {
            Id = "test.us",
            Name = "Test Channel",
            Country = "US",
            StreamUrl = "http://test/stream.m3u8"
        };

        Assert.Equal("test.us", channel.Id);
        Assert.Equal("Test Channel", channel.Name);
        Assert.Equal("US", channel.Country);
        Assert.Equal("http://test/stream.m3u8", channel.StreamUrl);
    }

    [Fact]
    public void OptionalProperties_DefaultCorrectly()
    {
        var channel = new ChannelWithStream
        {
            Id = "test",
            Name = "Test",
            Country = "US",
            StreamUrl = "http://test"
        };

        Assert.Empty(channel.Categories);
        Assert.Null(channel.Quality);
        Assert.Null(channel.UserAgent);
        Assert.Null(channel.Referrer);
        Assert.False(channel.IsNsfw);
    }
}
