namespace FoxIPTV.Services;

using System.IO.Compression;
using System.Formats.Tar;
using System.Reflection;

public static class VlcNativeManager
{
    private static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".foxiptv", "vlc");

    public static string? LibPath { get; private set; }
    public static string? PluginPath { get; private set; }

    public static void EnsureExtracted()
    {
        var assembly = typeof(VlcNativeManager).Assembly;
        using var stream = assembly.GetManifestResourceStream("FoxIPTV.vlc-native.tar.gz");
        if (stream is null) return; // Dev mode — no embedded VLC, use system/NuGet

        // Use InformationalVersion (includes pre-release tag) so each alpha gets its own cache
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "dev";
        // Sanitize: InformationalVersion may contain '+commithash' — strip it for path safety
        var plusIdx = version.IndexOf('+');
        if (plusIdx >= 0) version = version[..plusIdx];
        var versionDir = Path.Combine(BaseDir, version);
        var markerFile = Path.Combine(versionDir, ".extracted");

        if (File.Exists(markerFile))
        {
            SetPaths(versionDir);
            return;
        }

        // Clean old versions
        if (Directory.Exists(BaseDir))
        {
            foreach (var dir in Directory.GetDirectories(BaseDir))
            {
                if (dir != versionDir)
                {
                    try { Directory.Delete(dir, true); }
                    catch { /* old version may be in use */ }
                }
            }
        }

        Directory.CreateDirectory(versionDir);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzip, versionDir, overwriteFiles: true);

        File.WriteAllText(markerFile, DateTime.UtcNow.ToString("O"));
        SetPaths(versionDir);
    }

    private static void SetPaths(string versionDir)
    {
        LibPath = versionDir;
        PluginPath = Path.Combine(versionDir, "plugins");
    }
}
