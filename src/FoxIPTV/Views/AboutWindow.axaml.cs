// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Classes;

    public partial class AboutWindow : Window
    {
        private const string LicensePrefix = "FoxIPTV.Licenses.";

        public AboutWindow()
        {
            InitializeComponent();

            TitleLabel.Text += $" V{TvCore.Version}";

            PatreonButton.Click += (sender, args) => OpenLink("https://www.patreon.com/FoxCouncil");
            GithubButton.Click += (sender, args) => OpenLink("https://github.com/FoxCouncil/FoxIPTV");

            IconAttributionButton.Click += (sender, args) => OpenLink("http://p.yusukekamiyamane.com/");

            foreach (var resource in typeof(AboutWindow).Assembly.GetManifestResourceNames().Where(x => x.StartsWith(LicensePrefix, StringComparison.Ordinal)).OrderBy(x => x, StringComparer.Ordinal))
            {
                var name = resource.Substring(LicensePrefix.Length);
                var link = new Button { Content = name, HorizontalAlignment = HorizontalAlignment.Center };

                link.Classes.Add("link");
                link.Click += (sender, args) => OpenLicense(resource, name);

                LicenseLinks.Children.Add(link);
            }

            CloseButton.Click += (sender, args) => Close();
        }

        private static void OpenLink(string address)
        {
            try
            {
                Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[AboutWindow] Unable to open the link: {ex.Message}");
            }
        }

        private static void OpenLicense(string resource, string name)
        {
            try
            {
                var path = Path.Combine(TvCore.TempPath, name);

                using (var source = typeof(AboutWindow).Assembly.GetManifestResourceStream(resource))
                using (var file = File.Create(path))
                {
                    source.CopyTo(file);
                }

                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[AboutWindow] Unable to open {name}: {ex.Message}");
            }
        }
    }
}
