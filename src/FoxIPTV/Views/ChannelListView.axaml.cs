using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FoxIPTV.Views;

public partial class ChannelListView : UserControl
{
    public ChannelListView()
    {
        InitializeComponent();
    }

    private async void OnCopyStreamLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url } &&
            TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(url);
        }
    }
}
