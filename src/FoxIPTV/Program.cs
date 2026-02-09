using Avalonia;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using System;

namespace FoxIPTV;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
    }
}
