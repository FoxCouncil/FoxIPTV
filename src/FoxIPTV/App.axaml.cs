// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV
{
    using System;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Markup.Xaml;
    using Classes;
    using Views;

    public partial class App : Application
    {
        /// <summary>Set when the user asked to switch provider; the application restarts after it shuts down</summary>
        public static bool RestartRequested { get; set; }

        /// <summary>The desktop lifetime, for shutting down</summary>
        public static IClassicDesktopStyleApplicationLifetime Desktop => Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

        /// <inheritdoc/>
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <inheritdoc/>
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                desktop.ShutdownRequested += (sender, args) => TvCore.Settings.Save();

                _ = StartAsync(desktop);
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>Pick a provider, then open the main window, or quit if the user gives up</summary>
        private static async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (TvCore.Services.Count == 0)
            {
                await Dialogs.Message(null, "No content providers could be loaded, check the log for details.", "Fox IPTV");

                desktop.Shutdown();

                return;
            }

            if (!await ProviderWindow.ChooseProvider())
            {
                desktop.Shutdown();

                return;
            }

            var main = new MainWindow();

            desktop.MainWindow = main;

            main.Start();
        }

        /// <summary>The macOS application menu's About item</summary>
        private void AboutMenuItem_Click(object sender, EventArgs e)
        {
            new AboutWindow().Show();
        }
    }
}
