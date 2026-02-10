namespace FoxIPTV.Tests.ViewModels;

using FoxIPTV.ViewModels;

public class TrackOptionTests
{
    [Fact]
    public void Record_StoresProperties()
    {
        var option = new TrackOption(1, "English", true);

        Assert.Equal(1, option.Id);
        Assert.Equal("English", option.Name);
        Assert.True(option.IsActive);
    }

    [Fact]
    public void Record_Equality_WorksByValue()
    {
        var a = new TrackOption(1, "English", true);
        var b = new TrackOption(1, "English", true);
        var c = new TrackOption(2, "French", false);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
