// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.IO;

    public static class ProviderStore
    {
        private static string PathFor(string providerId) => Path.Combine(TvCore.UserStoragePath, $"pdata-{Web.SafeFilename(providerId)}");

        public static JObject Load(string providerId)
        {
            var path = PathFor(providerId);

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JObject.Parse(File.ReadAllText(path).Unprotect());
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[ProviderStore] Unable to read {path}: {ex.Message}");

                return null;
            }
        }

        public static void Save(string providerId, JObject data)
        {
            var path = PathFor(providerId);

            try
            {
                File.WriteAllText(path, (data ?? new JObject()).ToString().Protect());
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[ProviderStore] Unable to write {path}: {ex.Message}");
            }
        }

        public static void Delete(string providerId)
        {
            var path = PathFor(providerId);

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[ProviderStore] Unable to delete {path}: {ex.Message}");
            }
        }
    }
}
