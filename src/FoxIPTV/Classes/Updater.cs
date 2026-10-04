// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    public sealed class UpdateOffer
    {
        public string Version { get; set; }

        public string Name { get; set; }

        public string Address { get; set; }

        public string Checksum { get; set; }
    }

    public static class Updater
    {
        private const string LatestRelease = "https://api.github.com/repos/FoxCouncil/FoxIPTV/releases/latest";

        private const string ChecksumsName = "SHA256SUMS";

        private static readonly HttpClient Client = CreateClient();

        public static event Action AvailableChanged;

        public static UpdateOffer Available { get; private set; }

        public static bool IsEnabled => Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>().Any(x => x.Key == "SelfUpdate" && x.Value == "true") == true;

        private static string ExePath => Environment.ProcessPath;

        private static string OldPath => ExePath + ".old";

        private static string NewPath => ExePath + ".new";

        private static string AppBundle
        {
            get
            {
                var bundle = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ExePath)));

                return bundle != null && bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ? bundle : null;
            }
        }

        private static string AssetSuffix
        {
            get
            {
                var arch = RuntimeInformation.OSArchitecture;

                if (OperatingSystem.IsWindows() && arch == Architecture.X64)
                {
                    return "-windows-x64.exe";
                }

                if (OperatingSystem.IsLinux() && arch == Architecture.X64)
                {
                    return "-linux-x64";
                }

                if (OperatingSystem.IsMacOS() && arch == Architecture.Arm64 && AppBundle != null)
                {
                    return "-macos-arm64.zip";
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

        public static void CheckInBackground()
        {
            if (!IsEnabled)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                ClearLeftovers();

                try
                {
                    await CheckAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[Updater] Update check failed: {ex.GetType().Name}: {ex.Message}");
                }
            });
        }

        public static async Task<UpdateOffer> CheckAsync()
        {
            var suffix = AssetSuffix;

            if (suffix == null)
            {
                TvCore.LogInfo($"[Updater] No release build for {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");

                return null;
            }

            var release = JObject.Parse(await Client.GetStringAsync(LatestRelease).ConfigureAwait(false));
            var latest = release["tag_name"]?.ToString().TrimStart('v') ?? string.Empty;
            UpdateOffer offer = null;

            if (IsNewer(latest, TvCore.Version))
            {
                var assets = release["assets"] as JArray ?? new JArray();
                var package = assets.FirstOrDefault(x => x["name"]?.ToString().EndsWith(suffix, StringComparison.OrdinalIgnoreCase) == true);
                var checksums = assets.FirstOrDefault(x => x["name"]?.ToString() == ChecksumsName);

                if (package != null && checksums != null)
                {
                    var name = package["name"].ToString();
                    var checksum = ExpectedHash(await Client.GetStringAsync(checksums["browser_download_url"].ToString()).ConfigureAwait(false), name);

                    if (checksum != null)
                    {
                        offer = new UpdateOffer { Version = latest, Name = name, Address = package["browser_download_url"].ToString(), Checksum = checksum };
                    }
                }

                if (offer == null)
                {
                    TvCore.LogError($"[Updater] Release {latest} has no {suffix} build with a checksum");
                }
            }

            TvCore.LogInfo(offer == null ? $"[Updater] {TvCore.Version} is current, the latest release is {latest}" : $"[Updater] {latest} is available");

            Available = offer;

            AvailableChanged?.Invoke();

            return offer;
        }

        public static async Task InstallAsync(UpdateOffer offer)
        {
            ClearLeftovers();

            TvCore.LogInfo($"[Updater] Downloading {offer.Name}");

            using (var response = await Client.GetAsync(offer.Address, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                using (var file = File.Create(NewPath))
                {
                    await response.Content.CopyToAsync(file).ConfigureAwait(false);
                }
            }

            string actual;

            using (var file = File.OpenRead(NewPath))
            {
                actual = Convert.ToHexString(SHA256.HashData(file));
            }

            if (!string.Equals(actual, offer.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(NewPath);

                throw new InvalidDataException($"{offer.Name} checksum {actual} does not match {offer.Checksum}");
            }

            if (OperatingSystem.IsMacOS())
            {
                SwapBundle();
            }
            else
            {
                SwapExe();
            }

            Available = null;

            AvailableChanged?.Invoke();

            TvCore.LogInfo($"[Updater] {offer.Version} installed, it runs from the next start");
        }

        private static void SwapExe()
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(NewPath, File.GetUnixFileMode(ExePath));
            }

            File.Move(ExePath, OldPath, true);

            try
            {
                File.Move(NewPath, ExePath);
            }
            catch (Exception)
            {
                File.Move(OldPath, ExePath, true);

                throw;
            }
        }

        [SupportedOSPlatform("macos")]
        private static void SwapBundle()
        {
            var bundle = AppBundle;
            var unpacked = bundle + ".new";
            var old = bundle + ".old";

            try
            {
                ZipFile.ExtractToDirectory(NewPath, unpacked);
            }
            finally
            {
                File.Delete(NewPath);
            }

            var fresh = Path.Combine(unpacked, Path.GetFileName(bundle));
            var binary = Path.Combine(fresh, "Contents", "MacOS", Path.GetFileName(ExePath));

            if (!File.Exists(binary))
            {
                Directory.Delete(unpacked, true);

                throw new InvalidDataException($"The update has no {Path.GetFileName(bundle)}/Contents/MacOS/{Path.GetFileName(ExePath)}");
            }

            File.SetUnixFileMode(binary, File.GetUnixFileMode(ExePath));

            Directory.Move(bundle, old);

            try
            {
                Directory.Move(fresh, bundle);
            }
            catch (Exception)
            {
                Directory.Move(old, bundle);

                throw;
            }
            finally
            {
                Directory.Delete(unpacked, true);
            }
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

        private static void ClearLeftovers()
        {
            var bundle = OperatingSystem.IsMacOS() ? AppBundle : null;
            var folders = bundle == null ? Array.Empty<string>() : new[] { bundle + ".old", bundle + ".new" };

            foreach (var leftover in new[] { OldPath, NewPath }.Concat(folders))
            {
                try
                {
                    if (Directory.Exists(leftover))
                    {
                        Directory.Delete(leftover, true);
                    }
                    else
                    {
                        File.Delete(leftover);
                    }
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[Updater] Could not remove {leftover}: {ex.Message}");
                }
            }
        }
    }
}
