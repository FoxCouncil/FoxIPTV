// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV
{
    using System;
    using System.Runtime.InteropServices;
    using Avalonia;
    using Classes;

    public static class Program
    {
        /// <summary>The main entry point for the application.</summary>
        [STAThread]
        public static void Main(string[] args)
        {
            TvCore.LogInfo("[.NET] Main(): Starting Application...");

            if (OperatingSystem.IsMacOS())
            {
                SetMacBundleName("FoxIPTV");
            }

#if DEBUG
            if (args.Length > 0 && args[0] == "--test-provider")
            {
                ProviderSelfTest.Run(args);

                return;
            }
#endif

            VlcNativeManager.EnsureExtracted();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);

            TvCore.LogInfo("[.NET] Main(): Quitting Application...");

            if (App.RestartRequested && Environment.ProcessPath != null)
            {
                TvCore.LogInfo("[.NET] Main(): Restarting for provider switch");

                System.Diagnostics.Process.Start(Environment.ProcessPath);
            }
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
        }

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
            catch (Exception)
            {
            }
        }

        [DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr objc_getClass(string name);

        [DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_utf8(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string arg);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_2ptr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);
    }
}
