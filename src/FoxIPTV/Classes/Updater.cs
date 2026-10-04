// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Formats.Tar;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    public static class Updater
    {
        private const string LatestRelease = "https://api.github.com/repos/FoxCouncil/FoxIPTV/releases/latest";

        private const string ChecksumsName = "SHA256SUMS";

        private const string OldSuffix = ".old";

        private const string StagingFolder = ".update";

        private const string LeftoversFile = ".update-leftovers";

        private static readonly HttpClient Client = CreateClient();

        private static string AppFolder => AppContext.BaseDirectory;

        private static string LeftoversPath => Path.Combine(AppFolder, LeftoversFile);

        public static bool IsEnabled => Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>().Any(x => x.Key == "SelfUpdate" && x.Value == "true") == true;

        private static string AssetSuffix
        {
            get
            {
                var arch = RuntimeInformation.OSArchitecture;

                if (OperatingSystem.IsWindows() && arch == Architecture.X64)
                {
                    return "-windows-x64.zip";
                }

                if (OperatingSystem.IsLinux() && arch == Architecture.X64)
                {
                    return "-linux-x64.tar.gz";
                }

                if (OperatingSystem.IsMacOS() && arch == Architecture.Arm64)
                {
                    return "-macos-arm64.tar.gz";
                }

                return null;
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Web.UserAgent);

            return client;
        }

        public static void UpdateInBackground()
        {
            if (!IsEnabled)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Update().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[Updater] Update failed: {ex.GetType().Name}: {ex.Message}");
                }
            });
        }

        private static async Task Update()
        {
            ClearLeftovers();

            var suffix = AssetSuffix;

            if (suffix == null)
            {
                TvCore.LogInfo($"[Updater] No release build for {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");

                return;
            }

            var release = JObject.Parse(await Client.GetStringAsync(LatestRelease).ConfigureAwait(false));
            var latest = release["tag_name"]?.ToString().TrimStart('v') ?? string.Empty;

            if (!IsNewer(latest, TvCore.Version))
            {
                TvCore.LogInfo($"[Updater] {TvCore.Version} is current, the latest release is {latest}");

                return;
            }

            var assets = release["assets"] as JArray ?? new JArray();
            var package = assets.FirstOrDefault(x => x["name"]?.ToString().EndsWith(suffix, StringComparison.OrdinalIgnoreCase) == true);
            var checksums = assets.FirstOrDefault(x => x["name"]?.ToString() == ChecksumsName);

            if (package == null || checksums == null)
            {
                TvCore.LogError($"[Updater] Release {latest} has no {suffix} package or no {ChecksumsName}");

                return;
            }

            var name = package["name"].ToString();
            var expected = ExpectedHash(await Client.GetStringAsync(checksums["browser_download_url"].ToString()).ConfigureAwait(false), name);

            if (expected == null)
            {
                TvCore.LogError($"[Updater] {ChecksumsName} has no line for {name}");

                return;
            }

            if (!CanWrite())
            {
                TvCore.LogError($"[Updater] {AppFolder} is not writable, {latest} not installed");

                return;
            }

            TvCore.LogInfo($"[Updater] Downloading {name}");

            var download = Path.Combine(TvCore.TempPath, name);

            using (var response = await Client.GetAsync(package["browser_download_url"].ToString(), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                using (var file = File.Create(download))
                {
                    await response.Content.CopyToAsync(file).ConfigureAwait(false);
                }
            }

            try
            {
                string actual;

                using (var file = File.OpenRead(download))
                {
                    actual = Convert.ToHexString(SHA256.HashData(file));
                }

                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    TvCore.LogError($"[Updater] {name} checksum {actual} does not match {expected}, not installed");

                    return;
                }

                var staging = Path.Combine(AppFolder, StagingFolder);

                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, true);
                }

                Extract(download, staging);

                try
                {
                    Apply(staging);
                }
                finally
                {
                    Directory.Delete(staging, true);
                }
            }
            finally
            {
                File.Delete(download);
            }

            TvCore.LogInfo($"[Updater] {latest} installed, it runs from the next start");
        }

        public static bool IsNewer(string candidate, string current)
        {
            if (!TrySplit(candidate, out var candidateCore, out var candidateLabel) || !TrySplit(current, out var currentCore, out var currentLabel))
            {
                return false;
            }

            var core = candidateCore.CompareTo(currentCore);

            if (core != 0)
            {
                return core > 0;
            }

            if (candidateLabel.Length == 0 || currentLabel.Length == 0)
            {
                return candidateLabel.Length == 0 && currentLabel.Length > 0;
            }

            return string.CompareOrdinal(candidateLabel, currentLabel) > 0;
        }

        private static bool TrySplit(string version, out Version core, out string label)
        {
            var dash = (version ?? string.Empty).IndexOf('-');

            label = dash < 0 ? string.Empty : version.Substring(dash + 1);

            return Version.TryParse(dash < 0 ? version : version.Substring(0, dash), out core);
        }

        public static string ExpectedHash(string checksums, string name)
        {
            foreach (var line in checksums.Split('\n'))
            {
                var parts = line.Trim().Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 2 && parts[1].TrimStart('*') == name)
                {
                    return parts[0];
                }
            }

            return null;
        }

        private static void Extract(string archive, string folder)
        {
            Directory.CreateDirectory(folder);

            if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archive, folder);

                return;
            }

            using (var file = File.OpenRead(archive))
            using (var unzipped = new GZipStream(file, CompressionMode.Decompress))
            {
                TarFile.ExtractToDirectory(unzipped, folder, false);
            }
        }

        private static void Apply(string staging)
        {
            var replaced = new List<string>();
            var added = new List<string>();

            try
            {
                foreach (var source in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                {
                    var target = Path.Combine(AppFolder, Path.GetRelativePath(staging, source));

                    Directory.CreateDirectory(Path.GetDirectoryName(target));

                    if (File.Exists(target))
                    {
                        File.Move(target, target + OldSuffix, true);

                        replaced.Add(target);
                    }
                    else
                    {
                        added.Add(target);
                    }

                    File.Move(source, target);
                }

                File.WriteAllLines(LeftoversPath, replaced.Select(x => x + OldSuffix));
            }
            catch (Exception)
            {
                foreach (var target in added.Where(File.Exists))
                {
                    File.Delete(target);
                }

                foreach (var target in replaced)
                {
                    File.Move(target + OldSuffix, target, true);
                }

                throw;
            }
        }

        private static void ClearLeftovers()
        {
            if (!File.Exists(LeftoversPath))
            {
                return;
            }

            var remaining = new List<string>();

            foreach (var old in File.ReadAllLines(LeftoversPath).Where(x => x.EndsWith(OldSuffix, StringComparison.Ordinal)))
            {
                try
                {
                    File.Delete(old);
                }
                catch (Exception ex)
                {
                    remaining.Add(old);

                    TvCore.LogError($"[Updater] Could not remove {old}: {ex.Message}");
                }
            }

            if (remaining.Count > 0)
            {
                File.WriteAllLines(LeftoversPath, remaining);
            }
            else
            {
                File.Delete(LeftoversPath);
            }
        }

        private static bool CanWrite()
        {
            var probe = Path.Combine(AppFolder, $"{StagingFolder}-probe");

            try
            {
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
