// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Runtime.InteropServices;
    using Avalonia.Controls;
    using Classes;

    public static class WindowOpacity
    {
        private const int GwlExStyle = -20;

        private const int WsExLayered = 0x80000;

        private const int LwaAlpha = 0x2;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        public static bool IsSupported => OperatingSystem.IsWindows();

        public static void Apply(Window window, double opacity)
        {
            if (!IsSupported)
            {
                return;
            }

            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

            if (handle == IntPtr.Zero)
            {
                return;
            }

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
    }
}
