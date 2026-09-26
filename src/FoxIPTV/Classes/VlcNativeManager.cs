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

    /// <summary>Finds LibVLC's native libraries: unpacked from the release build's embedded archive, from the NuGet package in a build folder, or from the system</summary>
    public static class VlcNativeManager
    {
        private static readonly string BaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foxiptv", "vlc");

        private static bool _initialized;

        [DllImport("libc", SetLastError = true)]
        private static extern int setenv(string name, string value, int overwrite);

        /// <summary>The folder holding libvlc, null when the system or NuGet copy is used</summary>
        public static string LibPath { get; private set; }

        /// <summary>The folder holding LibVLC's plugins, null when the system or NuGet copy is used</summary>
        public static string PluginPath { get; private set; }

        /// <summary>Unpack the embedded LibVLC once per version; does nothing in a build without one</summary>
        public static void EnsureExtracted()
        {
            var assembly = typeof(VlcNativeManager).Assembly;

            using (var stream = assembly.GetManifestResourceStream("FoxIPTV.vlc-native.tar.gz"))
            {
                if (stream == null)
                {
                    // Build folder, LibVLC comes from NuGet or the system
                    return;
                }

                // Each version gets its own copy, the informational version carries the pre-release tag so every alpha is kept apart
                var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "dev";

                // It may end in +commithash, which is no good in a path
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

                // Old versions go, unless one is still running
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
                            // In use by another copy of FoxIPTV
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

        /// <summary>Load LibVLC's native libraries for this platform, once</summary>
        /// <remarks>
        /// macOS pre-loads the libraries from the unpacked copy or VLC.app and registers a resolver as a fallback, because Core.Initialize uses NativeLibrary.Load rather than DllImport.
        /// </remarks>
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

        /// <summary>The command line options every LibVLC instance gets on this platform</summary>
        /// <param name="options">The options FoxIPTV wants everywhere</param>
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

        /// <summary>What to tell the user when LibVLC will not load</summary>
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
