// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Runtime.InteropServices;
    using Avalonia.Controls;
    using Avalonia.Platform;
    using Classes;

    public static class WindowOpacity
    {
        private const int GwlExStyle = -20;

        private const int WsExLayered = 0x80000;

        private const int LwaAlpha = 0x2;

        private const int XaCardinal = 6;

        private const int PropModeReplace = 0;

        private static IntPtr _display;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XOpenDisplay(IntPtr name);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XInternAtom(IntPtr display, string name, bool onlyIfExists);

        [DllImport("libX11.so.6")]
        private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, ref nint data, int count);

        [DllImport("libX11.so.6")]
        private static extern int XDeleteProperty(IntPtr display, IntPtr window, IntPtr property);

        [DllImport("libX11.so.6")]
        private static extern int XFlush(IntPtr display);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
        private static extern IntPtr Selector(string name);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void SendDouble(IntPtr receiver, IntPtr selector, double value);

        public static bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

        public static void Apply(Window window, double opacity)
        {
            var handle = window.TryGetPlatformHandle();

            if (handle == null || handle.Handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    ApplyWindows(handle.Handle, opacity);
                }
                else if (OperatingSystem.IsLinux())
                {
                    ApplyX11(handle.Handle, opacity);
                }
                else if (OperatingSystem.IsMacOS() && handle is IMacOSTopLevelPlatformHandle mac && mac.NSWindow != IntPtr.Zero)
                {
                    SendDouble(mac.NSWindow, Selector("setAlphaValue:"), Math.Clamp(opacity, 0, 1));
                }
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[.NET] Setting the window transparency failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void ApplyWindows(IntPtr handle, double opacity)
        {
            var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();

            if (opacity >= 1)
            {
                if ((style & WsExLayered) != 0)
                {
                    SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style & ~WsExLayered));
                }

                return;
            }

            SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style | WsExLayered));

            if (!SetLayeredWindowAttributes(handle, 0, (byte)Math.Round(opacity * 255), LwaAlpha))
            {
                TvCore.LogError($"[.NET] SetLayeredWindowAttributes failed, error {Marshal.GetLastWin32Error()}");
            }
        }

        private static void ApplyX11(IntPtr window, double opacity)
        {
            if (_display == IntPtr.Zero)
            {
                _display = XOpenDisplay(IntPtr.Zero);

                if (_display == IntPtr.Zero)
                {
                    return;
                }
            }

            var property = XInternAtom(_display, "_NET_WM_WINDOW_OPACITY", false);

            if (opacity >= 1)
            {
                XDeleteProperty(_display, window, property);
            }
            else
            {
                nint value = (nint)(uint)Math.Round(Math.Clamp(opacity, 0, 1) * uint.MaxValue);

                XChangeProperty(_display, window, property, XaCardinal, 32, PropModeReplace, ref value, 1);
            }

            XFlush(_display);
        }
    }
}
