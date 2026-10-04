// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Classes;

    public static class UpdatePrompt
    {
        private const string Title = "Fox IPTV";

        private static bool _busy;

        public static async Task Run(Window owner, Action restart)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;

            try
            {
                var offer = Updater.Available;

                if (offer == null)
                {
                    try
                    {
                        offer = await Updater.CheckAsync();
                    }
                    catch (Exception ex)
                    {
                        TvCore.LogError($"[Updater] Update check failed: {ex.Message}");

                        await Dialogs.Message(owner, $"Update check failed: {ex.Message}", Title);

                        return;
                    }

                    if (offer == null)
                    {
                        await Dialogs.Message(owner, $"Fox IPTV {TvCore.Version} is the latest version.", Title);

                        return;
                    }
                }

                if (!await Dialogs.YesNo(owner, $"Do you want to update from {TvCore.Version} to {offer.Version}?", Title))
                {
                    return;
                }

                try
                {
                    await Task.Run(() => Updater.InstallAsync(offer));
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[Updater] Update failed: {ex.GetType().Name}: {ex.Message}");

                    await Dialogs.Message(owner, $"Update failed: {ex.Message}", Title);

                    return;
                }

                if (await Dialogs.YesNo(owner, $"Updated to {offer.Version}. Close and restart now?", Title))
                {
                    restart();
                }
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
