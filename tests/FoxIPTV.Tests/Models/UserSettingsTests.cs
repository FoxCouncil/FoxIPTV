namespace FoxIPTV.Tests.Models;

using FoxIPTV.Models;

public class UserSettingsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var settings = new UserSettings();

        Assert.Equal(80, settings.Volume);
        Assert.False(settings.IsMuted);
        Assert.Null(settings.LastChannelId);
        Assert.Empty(settings.FavoriteChannelIds);
        Assert.Null(settings.PreferredCountry);
        Assert.Null(settings.PreferredLanguage);
        Assert.True(settings.FilterNsfw);
        Assert.Equal(1280, settings.WindowWidth);
        Assert.Equal(720, settings.WindowHeight);
    }
}
