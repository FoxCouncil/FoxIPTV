// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Runtime.InteropServices;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media.Imaging;
    using Avalonia.Platform;
    using Microsoft.Win32;

    /// <summary>Keeps the notification area icon visible on both light and dark taskbars</summary>
    /// <remarks>The shipped icon is black line art. On a dark Windows taskbar it vanishes, so the same shape is redrawn in white when Windows reports a dark system theme.</remarks>
    public static class TrayIconArt
    {
        private static readonly Uri Source = new Uri("avares://FoxIPTV/Assets/FoxIPTV.ico");

        /// <summary>Whether the Windows taskbar and notification area are drawn dark</summary>
        public static bool TaskbarIsDark()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    // SystemUsesLightTheme covers the taskbar; AppsUseLightTheme covers app windows
                    return key?.GetValue("SystemUsesLightTheme") is int light && light == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The icon to show for the current taskbar theme</summary>
        public static WindowIcon ForTaskbar()
        {
            if (!TaskbarIsDark())
            {
                using (var stream = AssetLoader.Open(Source))
                {
                    return new WindowIcon(stream);
                }
            }

            return new WindowIcon(White());
        }

        /// <summary>The same shape painted pure white, keeping its alpha</summary>
        private static Bitmap White()
        {
            Bitmap source;

            using (var stream = AssetLoader.Open(Source))
            {
                source = new Bitmap(stream);
            }

            using (source)
            {
                var size = source.PixelSize;
                var white = new WriteableBitmap(size, new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Unpremul);

                using (var buffer = white.Lock())
                {
                    source.CopyPixels(buffer, AlphaFormat.Unpremul);

                    var pixels = new byte[buffer.RowBytes * size.Height];

                    Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);

                    for (var i = 0; i < pixels.Length; i += 4)
                    {
                        if (pixels[i + 3] == 0)
                        {
                            continue;
                        }

                        pixels[i] = 255;
                        pixels[i + 1] = 255;
                        pixels[i + 2] = 255;
                    }

                    Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
                }

                return white;
            }
        }
    }
}
