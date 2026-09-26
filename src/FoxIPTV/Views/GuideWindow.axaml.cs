// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using Avalonia.Controls;
    using Avalonia.Threading;
    using Classes;

    /// <summary>The programme guide window: a clock, a reset button and the drawn guide</summary>
    public partial class GuideWindow : Window
    {
        private readonly DispatcherTimer _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        /// <inheritdoc/>
        public GuideWindow()
        {
            InitializeComponent();

            Guide.AttachScrollBar(GuideScrollBar);

            ResetViewButton.Click += (sender, args) => Guide.ResetView();

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

                // Hidden, not disposed, so it opens again as it was
                args.Cancel = true;

                TvCore.Settings.GuideOpen = false;
                TvCore.Settings.Save();

                Hide();
            };
        }
    }
}
