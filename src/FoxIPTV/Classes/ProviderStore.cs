// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.IO;

    /// <summary>Remembers what the user typed into a provider's fields, encrypted to the Windows user with DPAPI</summary>
    public static class ProviderStore
    {
        /// <summary>The file a provider's data lives in</summary>
        private static string PathFor(string providerId) => Path.Combine(TvCore.UserStoragePath, $"pdata-{Web.SafeFilename(providerId)}");

        /// <summary>Load the remembered field values</summary>
        /// <param name="providerId">The provider id</param>
        /// <returns>The values, or null if nothing was remembered or it could not be read</returns>
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

        /// <summary>Remember the field values</summary>
        /// <param name="providerId">The provider id</param>
        /// <param name="data">The values</param>
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

        /// <summary>Forget the field values</summary>
        /// <param name="providerId">The provider id</param>
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
