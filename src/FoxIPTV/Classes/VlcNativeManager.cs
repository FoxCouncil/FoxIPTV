// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Formats.Tar;
    using System.IO;
    using System.IO.Compression;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using LibVLCSharp.Shared;

    public static class VlcNativeManager
    {
        private static readonly string BaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foxiptv", "vlc");

        private static bool _initialized;

        [DllImport("libc", SetLastError = true)]
        private static extern int setenv(string name, string value, int overwrite);

        public static string LibPath { get; private set; }

        public static string PluginPath { get; private set; }

        public static void EnsureExtracted()
        {
            var assembly = typeof(VlcNativeManager).Assembly;

            using (var stream = assembly.GetManifestResourceStream("FoxIPTV.vlc-native.tar.gz"))
            {
                if (stream == null)
                {
                    return;
                }

                var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "dev";

                var plusIdx = version.IndexOf('+');

                if (plusIdx >= 0)
                {
                    version = version.Substring(0, plusIdx);
                }

                var versionDir = Path.Combine(BaseDir, version);
                var markerFile = Path.Combine(versionDir, ".extracted");

                if (File.Exists(markerFile))
                {
                    SetPaths(versionDir);

                    return;
                }

                if (Directory.Exists(BaseDir))
                {
                    foreach (var dir in Directory.GetDirectories(BaseDir))
                    {
                        if (dir == versionDir)
                        {
                            continue;
                        }

                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                Directory.CreateDirectory(versionDir);

                using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
                {
                    TarFile.ExtractToDirectory(gzip, versionDir, true);
                }

                File.WriteAllText(markerFile, DateTime.UtcNow.ToString("O"));

                SetPaths(versionDir);
            }
        }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                var vlcLibPath = LibPath ?? "/Applications/VLC.app/Contents/MacOS/lib/";
                var vlcPluginPath = PluginPath ?? "/Applications/VLC.app/Contents/MacOS/plugins/";

                setenv("DYLD_LIBRARY_PATH", vlcLibPath, 1);
                setenv("VLC_PLUGIN_PATH", vlcPluginPath, 1);

                NativeLibrary.Load(Path.Combine(vlcLibPath, "libvlccore.dylib"));
                NativeLibrary.Load(Path.Combine(vlcLibPath, "libvlc.dylib"));

                NativeLibrary.SetDllImportResolver(typeof(LibVLC).Assembly, (name, assembly, path) =>
                {
                    if (name == "libvlc" || name == "libvlccore")
                    {
                        if (NativeLibrary.TryLoad(Path.Combine(vlcLibPath, name + ".dylib"), out var handle))
                        {
                            return handle;
                        }
                    }

                    return IntPtr.Zero;
                });
            }
            else if (OperatingSystem.IsWindows() && LibPath != null)
            {
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", PluginPath);

                Core.Initialize(LibPath);
            }
            else if (OperatingSystem.IsLinux() && LibPath != null)
            {
                setenv("VLC_PLUGIN_PATH", PluginPath, 1);
                setenv("LD_LIBRARY_PATH", LibPath + ":" + (Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? string.Empty), 1);

                Core.Initialize(LibPath);
            }

            _initialized = true;
        }

        public static string[] Options(params string[] options)
        {
            var args = new List<string>(options) { "--no-video-title-show" };

            if (OperatingSystem.IsLinux())
            {
                args.Add("--no-xlib");

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
                {
                    args.Add("--vout=xcb_x11");
                }
            }

            return args.ToArray();
        }

        public static string HelpMessage()
        {
            if (OperatingSystem.IsLinux())
            {
                return "On Linux, install VLC: sudo apt install vlc libvlc-dev (Debian/Ubuntu) or sudo dnf install vlc vlc-devel (Fedora).";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "On macOS, install VLC from https://www.videolan.org or: brew install --cask vlc";
            }

            return "Make sure LibVLC is installed.";
        }

        private static void SetPaths(string versionDir)
        {
            LibPath = versionDir;
            PluginPath = Path.Combine(versionDir, "plugins");
        }
    }
}
