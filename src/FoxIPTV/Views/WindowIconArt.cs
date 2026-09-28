// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Avalonia.Controls;
    using Avalonia.Platform;
    using Avalonia.Styling;
    using Avalonia.Threading;

    public static unsafe class WindowIconArt
    {
        private const uint WmSetIcon = 0x0080;

        private const uint WmSettingChange = 0x001A;

        private const int IconSmall = 0;

        private const int IconBig = 1;

        private const int MetricIconWidth = 11;

        private const int MetricSmallIconWidth = 49;

        private static readonly Uri Source = new Uri("avares://FoxIPTV/Assets/FoxIPTV.ico");

        private static readonly Dictionary<(int Size, bool White), IntPtr> Icons = new Dictionary<(int Size, bool White), IntPtr>();

        private static readonly ConditionalWeakTable<Window, object> Hooked = new ConditionalWeakTable<Window, object>();

        private static byte[] _file;

        public static void FollowTheme()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            Window.WindowOpenedEvent.AddClassHandler<Window>((window, args) => Hook(window));
        }

        public static IntPtr LoadIcon(byte[] file, int size)
        {
            var count = BitConverter.ToUInt16(file, 4);
            var best = -1;
            var bestSize = 0;

            for (var i = 0; i < count; i++)
            {
                var frameSize = file[6 + i * 16] == 0 ? 256 : file[6 + i * 16];
                var better = best < 0 || frameSize >= size && (bestSize < size || frameSize < bestSize) || frameSize < size && bestSize < size && frameSize > bestSize;

                if (better)
                {
                    best = i;
                    bestSize = frameSize;
                }
            }

            if (best < 0)
            {
                return IntPtr.Zero;
            }

            var length = BitConverter.ToInt32(file, 6 + best * 16 + 8);
            var offset = BitConverter.ToInt32(file, 6 + best * 16 + 12);

            fixed (byte* frame = &file[offset])
            {
                return CreateIconFromResourceEx(frame, (uint)length, 1, 0x00030000, size, size, 0);
            }
        }

        public static byte[] Pixels(IntPtr icon, int size)
        {
            IconInfo info;

            if (icon == IntPtr.Zero || GetIconInfo(icon, &info) == 0)
            {
                return null;
            }

            try
            {
                return Read(info.Color, size);
            }
            finally
            {
                DeleteObject(info.Color);
                DeleteObject(info.Mask);
            }
        }

        public static IntPtr Whiten(IntPtr icon, int size)
        {
            IconInfo info;

            if (icon == IntPtr.Zero || GetIconInfo(icon, &info) == 0)
            {
                return IntPtr.Zero;
            }

            var screen = GetDC(IntPtr.Zero);

            try
            {
                var pixels = Read(info.Color, size);

                if (pixels == null)
                {
                    return IntPtr.Zero;
                }

                for (var i = 0; i < pixels.Length; i += 4)
                {
                    if (pixels[i + 3] != 0)
                    {
                        pixels[i] = 255;
                        pixels[i + 1] = 255;
                        pixels[i + 2] = 255;
                    }
                }

                var header = Header(size);
                void* bits;
                var color = CreateDIBSection(screen, &header, 0, &bits, IntPtr.Zero, 0);

                if (color == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                Marshal.Copy(pixels, 0, (IntPtr)bits, pixels.Length);

                var white = new IconInfo { IsIcon = 1, Mask = info.Mask, Color = color };
                var result = CreateIconIndirect(&white);

                DeleteObject(color);

                return result;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screen);
                DeleteObject(info.Color);
                DeleteObject(info.Mask);
            }
        }

        private static void Hook(Window window)
        {
            if (Hooked.TryGetValue(window, out _))
            {
                return;
            }

            Hooked.Add(window, null);

            Win32Properties.AddWndProcHookCallback(window, (IntPtr handle, uint message, IntPtr wParam, IntPtr lParam, ref bool handled) => OnMessage(window, handle, message, wParam, lParam, ref handled));

            window.ActualThemeVariantChanged += (sender, args) => Refresh(window);

            Refresh(window);
        }

        private static IntPtr OnMessage(Window window, IntPtr handle, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WmSettingChange && lParam != IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
            {
                Dispatcher.UIThread.Post(() => Refresh(window));

                return IntPtr.Zero;
            }

            if (message != WmSetIcon)
            {
                return IntPtr.Zero;
            }

            var big = wParam == (IntPtr)IconBig;
            var white = big ? TrayIconArt.TaskbarIsDark() : window.ActualThemeVariant == ThemeVariant.Dark;
            var size = GetSystemMetricsForDpi(big ? MetricIconWidth : MetricSmallIconWidth, GetDpiForWindow(handle));
            var icon = IconFor(size, white);

            if (icon == IntPtr.Zero)
            {
                handled = lParam == IntPtr.Zero;

                return IntPtr.Zero;
            }

            handled = true;

            return DefWindowProcW(handle, WmSetIcon, wParam, icon);
        }

        private static void Refresh(Window window)
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

            if (handle == IntPtr.Zero)
            {
                return;
            }

            SendMessageW(handle, WmSetIcon, (IntPtr)IconSmall, IntPtr.Zero);
            SendMessageW(handle, WmSetIcon, (IntPtr)IconBig, IntPtr.Zero);
        }

        private static IntPtr IconFor(int size, bool white)
        {
            if (Icons.TryGetValue((size, white), out var icon))
            {
                return icon;
            }

            try
            {
                if (_file == null)
                {
                    using (var stream = AssetLoader.Open(Source))
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);

                        _file = memory.ToArray();
                    }
                }

                icon = white ? Whiten(IconFor(size, false), size) : LoadIcon(_file, size);
            }
            catch (Exception)
            {
                icon = IntPtr.Zero;
            }

            if (icon != IntPtr.Zero)
            {
                Icons[(size, white)] = icon;
            }

            return icon;
        }

        private static byte[] Read(IntPtr bitmap, int size)
        {
            var pixels = new byte[size * size * 4];
            var header = Header(size);
            var screen = GetDC(IntPtr.Zero);

            try
            {
                fixed (byte* bits = pixels)
                {
                    return GetDIBits(screen, bitmap, 0, (uint)size, bits, &header, 0) == size ? pixels : null;
                }
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        private static BitmapInfo Header(int size)
        {
            return new BitmapInfo { Size = 40, Width = size, Height = -size, Planes = 1, BitCount = 32 };
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr SendMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr CreateIconFromResourceEx(byte* data, uint length, int isIcon, uint version, int width, int height, uint flags);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int GetIconInfo(IntPtr icon, IconInfo* info);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr CreateIconIndirect(IconInfo* info);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, void* bits, BitmapInfo* info, uint usage);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr CreateDIBSection(IntPtr dc, BitmapInfo* info, uint usage, void** bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern int DeleteObject(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct IconInfo
        {
            public int IsIcon;

            public int HotspotX;

            public int HotspotY;

            public IntPtr Mask;

            public IntPtr Color;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public uint Size;

            public int Width;

            public int Height;

            public ushort Planes;

            public ushort BitCount;

            public uint Compression;

            public uint SizeImage;

            public int XPelsPerMeter;

            public int YPelsPerMeter;

            public uint ColorsUsed;

            public uint ColorsImportant;

            public uint Mask0;

            public uint Mask1;

            public uint Mask2;

            public uint Mask3;
        }
    }
}
