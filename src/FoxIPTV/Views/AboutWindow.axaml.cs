// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Diagnostics;
    using Avalonia.Controls;
    using Classes;

    /// <summary>Shows information about the application, because</summary>
    public partial class AboutWindow : Window
    {
        /// <inheritdoc/>
        public AboutWindow()
        {
            InitializeComponent();

            TitleLabel.Text += $" V{TvCore.Version}";

            // The amazing person that shared their icons with the world!
            IconAttributionButton.Click += (sender, args) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("http://p.yusukekamiyamane.com/") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[AboutWindow] Unable to open the link: {ex.Message}");
                }
            };

            CloseButton.Click += (sender, args) => Close();
        }
    }
}
