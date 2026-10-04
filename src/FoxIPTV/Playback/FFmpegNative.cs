// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Classes;
    using FFmpeg.AutoGen;

    public static unsafe class FFmpegNative
    {
        private const string NativePrefix = "FoxIPTV.Native/";

        private static readonly object Lock = new object();

        private static bool _initialized;

        private static av_log_set_callback_callback _logCallback;

        public static bool IsAvailable { get; private set; }

        public static string Version { get; private set; }

        public static string Failure { get; private set; }

        public static bool Initialize()
        {
            lock (Lock)
            {
                if (_initialized)
                {
                    return IsAvailable;
                }

                _initialized = true;

                try
                {
                    var root = FindRoot();

                    if (OperatingSystem.IsLinux() && root != null)
                    {
                        LoadLibva(root);
                    }

                    ffmpeg.RootPath = root ?? string.Empty;

                    Version = ffmpeg.av_version_info();

                    _logCallback = OnLog;

                    ffmpeg.av_log_set_level(ffmpeg.AV_LOG_WARNING);
                    ffmpeg.av_log_set_callback(_logCallback);

                    IsAvailable = true;

                    TvCore.LogInfo($"[Player] FFmpeg {Version} loaded from {(string.IsNullOrEmpty(root) ? "the system search path" : root)}");
                }
                catch (Exception ex)
                {
                    Failure = ex.Message;
                    IsAvailable = false;

                    TvCore.LogError($"[Player] FFmpeg failed to load: {ex.GetType().Name}: {ex.Message}");
                }

                return IsAvailable;
            }
        }

        public static string Error(int code)
        {
            var buffer = stackalloc byte[256];

            ffmpeg.av_strerror(code, buffer, 256);

            return $"{Marshal.PtrToStringAnsi((IntPtr)buffer)} ({code})";
        }

        public static void Check(int code, string what)
        {
            if (code < 0)
            {
                throw new PlayerException($"{what} failed: {Error(code)}");
            }
        }

        private static void LoadLibva(string root)
        {
            foreach (var library in new[] { "libva.so.2", "libva-drm.so.2" })
            {
                if (NativeLibrary.TryLoad(library, out _))
                {
                    continue;
                }

                var bundled = Path.Combine(root, "libva", library);

                TvCore.LogInfo($"[Player] No {library} on this system, loading the bundled copy: {(NativeLibrary.TryLoad(bundled, out _) ? "ok" : "failed")}");
            }
        }

        private static string FileName(string library)
        {
            var version = ffmpeg.LibraryVersionMap[library];

            if (OperatingSystem.IsWindows())
            {
                return $"{library}-{version}.dll";
            }

            if (OperatingSystem.IsMacOS())
            {
                return $"lib{library}.{version}.dylib";
            }

            return $"lib{library}.so.{version}";
        }

        private static string FindRoot()
        {
            var wanted = FileName("avutil");
            var candidates = new List<string> { AppContext.BaseDirectory };

            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, wanted)))
            {
                try
                {
                    candidates.Add(Unpack());
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[Player] Unpacking the built-in FFmpeg failed: {ex.Message}");
                }
            }

            if (Environment.GetEnvironmentVariable("FLATPAK_ID") != null)
            {
                candidates.Add("/app/lib");
            }

            if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string searchDirectories)
            {
                candidates.AddRange(searchDirectories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
            }

            foreach (var directory in candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            {
                if (File.Exists(Path.Combine(directory, wanted)))
                {
                    return directory;
                }
            }

            TvCore.LogError($"[Player] {wanted} not found in {string.Join(", ", candidates)}; trying the system search path");

            return null;
        }

        private static string Unpack()
        {
            var assembly = typeof(FFmpegNative).Assembly;
            var names = assembly.GetManifestResourceNames().Where(x => x.StartsWith(NativePrefix, StringComparison.Ordinal)).ToList();

            if (names.Count == 0)
            {
                return null;
            }

            var parent = Path.Combine(TvCore.TempPath, "native");
            var folder = Path.Combine(parent, assembly.ManifestModule.ModuleVersionId.ToString("N"));

            foreach (var name in names)
            {
                var target = Path.Combine(folder, Path.Combine(name.Substring(NativePrefix.Length).Split('/', '\\')));

                if (File.Exists(target))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));

                var partial = $"{target}.{Environment.ProcessId}.part";

                using (var source = assembly.GetManifestResourceStream(name))
                using (var file = File.Create(partial))
                {
                    source.CopyTo(file);
                }

                try
                {
                    File.Move(partial, target);
                }
                catch (IOException) when (File.Exists(target))
                {
                    File.Delete(partial);
                }
            }

            foreach (var older in Directory.GetDirectories(parent).Where(x => !string.Equals(x, folder, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    Directory.Delete(older, true);
                }
                catch (Exception)
                {
                }
            }

            return folder;
        }

        private static void OnLog(void* avcl, int level, string format, byte* vl)
        {
            if (level > ffmpeg.av_log_get_level())
            {
                return;
            }

            try
            {
                const int size = 1024;

                var line = stackalloc byte[size];
                var prefix = 1;

                ffmpeg.av_log_format_line2(avcl, level, format, vl, line, size, &prefix);

                var text = Marshal.PtrToStringUTF8((IntPtr)line)?.TrimEnd();

                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                if (level <= ffmpeg.AV_LOG_ERROR)
                {
                    TvCore.LogError($"[FFmpeg] {text}");
                }
                else
                {
                    TvCore.LogInfo($"[FFmpeg] {text}");
                }
            }
            catch (Exception)
            {
            }
        }
    }

    public class PlayerException : Exception
    {
        public PlayerException(string message) : base(message)
        {
        }
    }
}
