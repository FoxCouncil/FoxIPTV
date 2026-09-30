// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using Avalonia.Controls;
    using Avalonia.Threading;
    using Classes;

    public partial class GuideWindow : Window
    {
        private readonly DispatcherTimer _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        public GuideWindow()
        {
            InitializeComponent();

            Guide.AttachScrollBar(GuideScrollBar);

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

            Closing += (sender, args) =>
            {
                if (args.CloseReason == WindowCloseReason.ApplicationShutdown || args.CloseReason == WindowCloseReason.OwnerWindowClosing || args.CloseReason == WindowCloseReason.OSShutdown)
                {
                    return;
                }

                args.Cancel = true;

                TvCore.Settings.GuideOpen = false;
                TvCore.Settings.Save();

                Hide();
            };
        }
    }
}
