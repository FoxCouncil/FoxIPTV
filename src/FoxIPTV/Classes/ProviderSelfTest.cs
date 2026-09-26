// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

#if DEBUG
namespace FoxIPTV.Classes
{
    using Newtonsoft.Json.Linq;
    using Services;
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>A headless exercise of a provider, for developing plugins without clicking through the UI</summary>
    /// <remarks>
    /// Usage: FoxIPTV.exe --test-provider &lt;id&gt; [Field=Value ...] [--search text]
    /// The report is written to %TEMP%\FoxIPTV\selftest-&lt;id&gt;.txt
    /// </remarks>
    public static class ProviderSelfTest
    {
        /// <summary>Run the self test described by the command line</summary>
        /// <param name="args">The full command line arguments</param>
        public static void Run(string[] args)
        {
            var id = args.Length > 1 ? args[1] : string.Empty;

            var report = new StringBuilder();

            var reportPath = Path.Combine(TvCore.TempPath, $"selftest-{Web.SafeFilename(id)}.txt");

            try
            {
                RunAsync(id, args.Skip(2).ToArray(), report).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                report.AppendLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
                report.AppendLine(ex.ToString());
            }

            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);

            TvCore.LogInfo($"[SelfTest] Report written to {reportPath}");
        }

        private static async Task RunAsync(string id, string[] options, StringBuilder report)
        {
            report.AppendLine($"Providers loaded: {string.Join(", ", TvCore.Services.Select(x => $"{x.Id} [{x.Capabilities}]"))}");

            foreach (var error in Services.Scripting.ScriptLoader.Errors)
            {
                report.AppendLine($"Plugin error: {error.Key}: {error.Value}");
            }

            if (!TvCore.SelectService(id))
            {
                report.AppendLine($"FAILED: no provider with id '{id}'");

                return;
            }

            var service = TvCore.CurrentService;

            report.AppendLine($"Provider: {service.Title} ({service.Id}) - {service.Description}");
            report.AppendLine($"Fields: {string.Join(", ", service.Fields.Select(x => $"{x.Key}:{x.Kind}{(x.Required ? "" : "?")}{(x.Default != null ? "=" + x.Default : "")}"))}");

            var data = new JObject();

            string search = "matrix";

            for (var i = 0; i < options.Length; i++)
            {
                if (options[i] == "--search" && i + 1 < options.Length)
                {
                    search = options[++i];

                    continue;
                }

                var eq = options[i].IndexOf('=');

                if (eq > 0)
                {
                    data[options[i].Substring(0, eq)] = options[i].Substring(eq + 1);
                }
            }

            foreach (var field in service.Fields.Where(x => x.Default != null && data[x.Key] == null))
            {
                data[field.Key] = field.Default;
            }

            service.Data = data;

            var started = DateTime.UtcNow;

            var authenticated = await service.IsAuthenticated();

            report.AppendLine($"IsAuthenticated: {authenticated} ({(DateTime.UtcNow - started).TotalMilliseconds:F0} ms)");

            if (!authenticated)
            {
                return;
            }

            if (service.Capabilities.HasFlag(ProviderCapabilities.LiveTv))
            {
                started = DateTime.UtcNow;

                var result = await service.Process();

                report.AppendLine($"Process: {result.Item1.Count} channel(s), {result.Item2.Count} programme(s) ({(DateTime.UtcNow - started).TotalSeconds:F1} s)");

                foreach (var channel in result.Item1.Take(5))
                {
                    report.AppendLine($"  Channel {channel.Index}: [{channel.Group}] {channel.Name} id={channel.Id} logo={channel.Logo} stream={channel.Stream}");
                }

                report.AppendLine($"  Distinct channel indexes: {result.Item1.Select(x => x.Index).Distinct().Count()}");

                var withGuide = result.Item1.Count(c => !string.IsNullOrEmpty(c.Id) && result.Item2.Any(p => p.Channel == c.Id));

                report.AppendLine($"  Channels with guide data: {withGuide}");

                foreach (var programme in result.Item2.Take(5))
                {
                    report.AppendLine($"  Programme: {programme.Channel} {programme.Start:u} - {programme.Stop:u} {programme.Title}");
                }
            }

            if (service.Capabilities.HasFlag(ProviderCapabilities.Library) && service is ILibraryProvider library)
            {
                var categories = await library.GetCategories();

                report.AppendLine($"Categories: {string.Join(", ", categories.Select(x => $"{x.Name} ({x.Id})"))}");

                LibraryItem sample = null;

                if (categories.Count > 0)
                {
                    var page = await library.Browse(categories[0].Id, 1);

                    report.AppendLine($"Browse '{categories[0].Name}' page {page.Page}/{page.TotalPages}: {page.Items.Count} item(s)");

                    foreach (var item in page.Items.Take(5))
                    {
                        report.AppendLine($"  {item.Kind}: {item} rating={item.Rating} poster={item.Poster}");
                    }

                    sample = page.Items.FirstOrDefault();
                }

                var results = await library.Search(search, 1);

                report.AppendLine($"Search '{search}' page {results.Page}/{results.TotalPages}: {results.Items.Count} item(s)");

                foreach (var item in results.Items.Take(5))
                {
                    report.AppendLine($"  {item.Kind}: {item}");
                }

                var series = results.Items.FirstOrDefault(x => x.Kind == LibraryItemKind.Series) ?? sample;

                if (series != null)
                {
                    var details = await library.GetDetails(series.Id, series.Kind);

                    report.AppendLine($"Details '{details}': kind={details.Kind} duration={details.DurationMinutes} seasons={details.Seasons.Count} overview={Truncate(details.Overview)}");

                    LibraryItem playable = details;

                    if (details.Kind == LibraryItemKind.Series && details.Seasons.Count > 0)
                    {
                        var episodes = await library.GetEpisodes(details.Id, details.Seasons[0].Number);

                        report.AppendLine($"Episodes of {details.Seasons[0]}: {episodes.Count}");

                        foreach (var episode in episodes.Take(3))
                        {
                            report.AppendLine($"  {episode.Subtitle} {episode.Title} still={episode.Poster}");
                        }

                        playable = episodes.FirstOrDefault();
                    }

                    if (playable != null && playable.IsPlayable)
                    {
                        var sources = await library.Resolve(playable);

                        report.AppendLine($"Resolve '{playable}': {sources.Count} source(s)");

                        foreach (var source in sources)
                        {
                            report.AppendLine($"  {source.Name} {source.Url}");
                        }
                    }
                }
            }

            report.AppendLine("OK");
        }

        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Length > 80 ? text.Substring(0, 80) + "..." : text;
        }
    }
}
#endif
