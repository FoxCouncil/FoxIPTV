using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using FoxIPTV.Services;
using FoxIPTV.ViewModels;
using FoxIPTV.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FoxIPTV;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };

            desktop.ShutdownRequested += async (_, _) =>
            {
                var settings = Services.GetRequiredService<ISettingsService>();
                await settings.SaveAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // HTTP client for iptv-org API
        services.AddHttpClient<IIptvService, IptvOrgService>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FoxIPTV/2.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Services
        services.AddSingleton<ICacheService, FileCacheService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ChannelListViewModel>();
        services.AddTransient<VideoPlayerViewModel>();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove = BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
