// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services.Scripting
{
    using Classes;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    /// <summary>Finds and compiles plugin scripts, both the ones shipped inside FoxIPTV and the user's own</summary>
    public static class ScriptLoader
    {
        /// <summary>The resource name prefix for scripts embedded in the executable</summary>
        private const string ResourcePrefix = "FoxIPTV.Plugins.";

        /// <summary>The folder the user drops their own .js plugins into</summary>
        public static string UserPluginPath => Path.Combine(TvCore.UserStoragePath, "plugins");

        /// <summary>The folder the built-in scripts are copied to for reference; nothing is loaded from here</summary>
        public static string ExamplesPath => Path.Combine(UserPluginPath, "examples");

        /// <summary>Scripts that failed to load during the last <see cref="LoadAll"/>, by origin</summary>
        public static Dictionary<string, string> Errors { get; } = new Dictionary<string, string>();

        /// <summary>Load every plugin; a user script with the same filename as a built-in replaces it</summary>
        /// <returns>The successfully compiled providers, in name order</returns>
        public static List<ScriptProvider> LoadAll()
        {
            Errors.Clear();

            var sources = new Dictionary<string, Tuple<string, string, bool>>(StringComparer.OrdinalIgnoreCase);

            var assembly = Assembly.GetExecutingAssembly();

            foreach (var resource in assembly.GetManifestResourceNames().Where(x => x.StartsWith(ResourcePrefix, StringComparison.Ordinal) && x.EndsWith(".js", StringComparison.OrdinalIgnoreCase)))
            {
                var filename = resource.Substring(ResourcePrefix.Length);

                using (var stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        continue;
                    }

                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        var source = reader.ReadToEnd();

                        sources[filename] = Tuple.Create(source, $"built-in {filename}", true);

                        WriteExample(filename, source);
                    }
                }
            }

            try
            {
                if (!Directory.Exists(UserPluginPath))
                {
                    Directory.CreateDirectory(UserPluginPath);
                }

                foreach (var file in Directory.GetFiles(UserPluginPath, "*.js", SearchOption.TopDirectoryOnly))
                {
                    var filename = Path.GetFileName(file);

                    sources[filename] = Tuple.Create(File.ReadAllText(file, Encoding.UTF8), file, false);
                }
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Plugins] Unable to scan {UserPluginPath}: {ex.Message}");
            }

            var providers = new List<ScriptProvider>();

            foreach (var entry in sources.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var provider = new ScriptProvider(entry.Value.Item1, entry.Value.Item2, entry.Value.Item3);

                    if (providers.Any(x => x.Id == provider.Id))
                    {
                        Errors[entry.Value.Item2] = $"Duplicate plugin id '{provider.Id}'";

                        TvCore.LogError($"[Plugins] {entry.Value.Item2}: duplicate plugin id '{provider.Id}', skipped");

                        continue;
                    }

                    TvCore.LogDebug($"[Plugins] Loaded '{provider.Title}' ({provider.Id} v{provider.Version}) from {provider.Origin}");

                    providers.Add(provider);
                }
                catch (ScriptException ex)
                {
                    Errors[entry.Value.Item2] = ex.Message;

                    TvCore.LogError($"[Plugins] {ex.Message}");
                }
            }

            return providers;
        }

        /// <summary>Keep a fresh copy of a built-in script where the user can read it</summary>
        private static void WriteExample(string filename, string source)
        {
            try
            {
                if (!Directory.Exists(ExamplesPath))
                {
                    Directory.CreateDirectory(ExamplesPath);
                }

                var path = Path.Combine(ExamplesPath, filename);

                if (!File.Exists(path) || File.ReadAllText(path, Encoding.UTF8) != source)
                {
                    File.WriteAllText(path, source, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Plugins] Unable to write example {filename}: {ex.Message}");
            }
        }
    }
}
