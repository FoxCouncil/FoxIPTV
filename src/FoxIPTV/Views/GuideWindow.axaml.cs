// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Threading;
    using Classes;

    public partial class GuideWindow : Window
    {
        private readonly DispatcherTimer _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        public GuideWindow()
        {
            InitializeComponent();

            Guide.AttachScrollBar(GuideScrollBar);
            Guide.AttachTimeBar(GuideTimeBar);

            AddHandler(KeyDownEvent, (sender, args) =>
            {
                if (!(args.Source is TextBox) && Guide.Navigate(args.Key))
                {
                    args.Handled = true;
                }
            }, RoutingStrategies.Tunnel);

            ResetViewButton.Click += (sender, args) => Guide.ResetView();

            SearchBox.TextChanged += (sender, args) =>
            {
                ClearSearchButton.IsVisible = !string.IsNullOrEmpty(SearchBox.Text);

                Guide.Search = SearchBox.Text;
            };

            ClearSearchButton.Click += (sender, args) =>
            {
                SearchBox.Text = string.Empty;

                SearchBox.Focus();
            };

            _clock.Tick += (sender, args) => DateTimeLabel.Text = $"{DateTime.Now:F}";
            _clock.Start();

            DateTimeLabel.Text = $"{DateTime.Now:F}";

            Opened += (sender, args) => Guide.Focus();

            Dialogs.HideOnClose(this, () => TvCore.Settings.GuideOpen = false);
        }
    }
}
