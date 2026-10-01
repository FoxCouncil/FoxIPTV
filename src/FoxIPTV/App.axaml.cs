// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV
{
    using System;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Markup.Xaml;
    using Classes;
    using Views;

    public partial class App : Application
    {
        public static bool RestartRequested { get; set; }

        public static IClassicDesktopStyleApplicationLifetime Desktop => Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            WindowIconArt.FollowTheme();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownRequested += (sender, args) => TvCore.Settings.Save();

                _ = StartAsync(desktop);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!await ProviderWindow.ChooseProvider())
            {
                desktop.Shutdown();

                return;
            }

            var main = new MainWindow();

            desktop.MainWindow = main;

            main.Start();
        }

        private void AboutMenuItem_Click(object sender, EventArgs e)
        {
            new AboutWindow().Show();
        }
    }
}
