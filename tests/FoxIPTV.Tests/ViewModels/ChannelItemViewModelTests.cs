namespace FoxIPTV.Tests.ViewModels;

using FoxIPTV.ViewModels;

public class ChannelItemViewModelTests
{
    [Fact]
    public void CategoryDisplay_WithCategories_ShowsCategories()
    {
        var vm = new ChannelItemViewModel
        {
            Id = "test",
            Name = "Test",
            Country = "US",
            StreamUrl = "http://test",
            Categories = ["News", "Sports"]
        };

        Assert.Equal("News, Sports", vm.CategoryDisplay);
    }

    [Fact]
    public void CategoryDisplay_WithNoCategories_ShowsCountry()
    {
        var vm = new ChannelItemViewModel
        {
            Id = "test",
            Name = "Test",
            Country = "US",
            CountryName = "United States",
            StreamUrl = "http://test",
            Categories = []
        };

        Assert.Equal("United States", vm.CategoryDisplay);
    }

    [Fact]
    public void IsFavorite_RaisesPropertyChanged()
    {
        var vm = new ChannelItemViewModel
        {
            Id = "test",
            Name = "Test",
            Country = "US",
            StreamUrl = "http://test",
            Categories = []
        };

        bool propertyChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChannelItemViewModel.IsFavorite))
                propertyChanged = true;
        };

        vm.IsFavorite = true;

        Assert.True(propertyChanged);
    }
}
