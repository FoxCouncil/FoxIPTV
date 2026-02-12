using Avalonia.Controls;
using Avalonia.Input;
using FoxIPTV.ViewModels;

namespace FoxIPTV.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        VideoPlayerView.VideoDoubleTapped += OnVideoDoubleTapped;

        Loaded += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.FullScreenRequested += OnFullScreenRequested;
                await vm.InitializeCommand.ExecuteAsync(null);
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            base.OnKeyDown(e);
            return;
        }

        // Don't handle single-key shortcuts when a text input has focus
        var isTyping = FocusManager?.GetFocusedElement() is TextBox;

        switch (e.Key)
        {
            case Key.F11:
                vm.ToggleFullScreenCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape when vm.IsFullScreen:
                vm.IsFullScreen = false;
                e.Handled = true;
                break;

            case Key.Space when !isTyping && vm.VideoPlayer.IsPlaying:
                vm.VideoPlayer.TogglePlayPauseCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.M when !isTyping:
                vm.VideoPlayer.ToggleMuteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.L when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.ToggleChannelListCommand.Execute(null);
                e.Handled = true;
                break;

            default:
                base.OnKeyDown(e);
                break;
        }
    }

    private void OnVideoDoubleTapped(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ToggleFullScreenCommand.Execute(null);
        }
    }

    private void OnFullScreenRequested(bool fullScreen)
    {
        WindowState = fullScreen ? WindowState.FullScreen : WindowState.Normal;
    }

    private void OnAudioTrackItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: int trackId } && DataContext is MainWindowViewModel vm)
        {
            vm.VideoPlayer.SelectAudioTrackCommand.Execute(trackId);
            AudioTrackButton.Flyout?.Hide();
        }
    }

    private void OnSubtitleTrackItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: int trackId } && DataContext is MainWindowViewModel vm)
        {
            vm.VideoPlayer.SelectSubtitleTrackCommand.Execute(trackId);
            SubtitleButton.Flyout?.Hide();
        }
    }
}
