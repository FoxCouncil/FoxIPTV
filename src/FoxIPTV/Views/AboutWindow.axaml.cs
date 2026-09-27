// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Diagnostics;
    using Avalonia.Controls;
    using Classes;

    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            TitleLabel.Text += $" V{TvCore.Version}";

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
