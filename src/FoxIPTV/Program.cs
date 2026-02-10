using Avalonia;
using FoxIPTV.Services;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using System;
using System.Runtime.InteropServices;

namespace FoxIPTV;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsMacOS())
            SetMacBundleName("FoxIPTV");

        VlcNativeManager.EnsureExtracted();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
    }

    /// <summary>
    /// Patches NSBundle.mainBundle.infoDictionary with CFBundleName so Avalonia
    /// shows "FoxIPTV" in the macOS menu bar instead of "Avalonia Application".
    /// </summary>
    private static void SetMacBundleName(string name)
    {
        try
        {
            var bundleCls = objc_getClass("NSBundle");
            var mainBundle = objc_msgSend(bundleCls, sel_registerName("mainBundle"));
            var infoDict = objc_msgSend(mainBundle, sel_registerName("infoDictionary"));

            var nsCls = objc_getClass("NSString");
            var key = objc_msgSend_utf8(nsCls, sel_registerName("stringWithUTF8String:"), "CFBundleName");
            var value = objc_msgSend_utf8(nsCls, sel_registerName("stringWithUTF8String:"), name);

            objc_msgSend_2ptr(infoDict, sel_registerName("setObject:forKey:"), value, key);
        }
        catch
        {
            // Non-fatal: menu bar will show default name
        }
    }

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_utf8(IntPtr receiver, IntPtr selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string arg);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_2ptr(IntPtr receiver, IntPtr selector,
        IntPtr arg1, IntPtr arg2);
}
