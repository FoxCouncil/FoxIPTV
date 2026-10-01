// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Avalonia.Media;
    using Classes;

    public static class Dialogs
    {
        public static Task Message(Window owner, string text, string title)
        {
            return Show(owner, text, title, "OK", null);
        }

        public static Task<bool> YesNo(Window owner, string text, string title)
        {
            return Show(owner, text, title, "Yes", "No");
        }

        public static void HideOnClose(Window window, Action hidden)
        {
            window.Closing += (sender, args) =>
            {
                if (args.CloseReason == WindowCloseReason.ApplicationShutdown || args.CloseReason == WindowCloseReason.OwnerWindowClosing || args.CloseReason == WindowCloseReason.OSShutdown)
                {
                    return;
                }

                args.Cancel = true;

                hidden();

                TvCore.Settings.Save();

                window.Hide();
            };
        }

        private static async Task<bool> Show(Window owner, string text, string title, string yes, string no)
        {
            var result = false;

            var window = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                ShowInTaskbar = owner == null,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Icon = owner?.Icon
            };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };

            var yesButton = new Button { Content = yes, IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };

            yesButton.Click += (sender, args) =>
            {
                result = true;
                window.Close();
            };

            buttons.Children.Add(yesButton);

            if (no != null)
            {
                var noButton = new Button { Content = no, IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };

                noButton.Click += (sender, args) => window.Close();

                buttons.Children.Add(noButton);
            }

            window.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                MaxWidth = 520,
                Children =
                {
                    new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                    buttons
                }
            };

            if (owner != null && owner.IsVisible)
            {
                await window.ShowDialog(owner);
            }
            else
            {
                var closed = new TaskCompletionSource<bool>();

                window.Closed += (sender, args) => closed.TrySetResult(true);
                window.Show();

                await closed.Task;
            }

            return result;
        }
    }
}
