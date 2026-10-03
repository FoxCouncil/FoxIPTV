// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Threading.Tasks;
    using Velopack;
    using Velopack.Sources;

    public static class Updater
    {
        private const string ReleasesAddress = "https://github.com/FoxCouncil/FoxIPTV";

        private static readonly Lazy<UpdateManager> Manager = new Lazy<UpdateManager>(() => new UpdateManager(new GithubSource(ReleasesAddress, null, false, null), null, null));

        public static bool IsInstalled
        {
            get
            {
                try
                {
                    return Manager.Value.IsInstalled;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static void DownloadInBackground()
        {
            if (!IsInstalled)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var manager = Manager.Value;

                    var pending = manager.UpdatePendingRestart;

                    if (pending != null)
                    {
                        TvCore.LogInfo($"[Updater] {pending.Version} is downloaded and installs at the next start");

                        return;
                    }

                    var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);

                    if (update == null)
                    {
                        TvCore.LogInfo($"[Updater] {manager.CurrentVersion} is the newest release");

                        return;
                    }

                    TvCore.LogInfo($"[Updater] Downloading {update.TargetFullRelease.Version}");

                    await manager.DownloadUpdatesAsync(update).ConfigureAwait(false);

                    TvCore.LogInfo($"[Updater] {update.TargetFullRelease.Version} downloaded, installs at the next start");
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[Updater] Update check failed: {ex.Message}");
                }
            });
        }
    }
}
