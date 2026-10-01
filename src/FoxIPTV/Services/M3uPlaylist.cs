// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Classes;
    using Newtonsoft.Json.Linq;

    public class M3uPlaylist : IService
    {
        public string Id => "m3u";

        public string Title => "M3U Playlist";

        public List<ProviderField> Fields { get; } = new List<ProviderField>
        {
            ProviderField.Url("Playlist URL"),
            ProviderField.Url("Guide URL", required: false),
            ProviderField.Text("Cache Hours", "6", false)
        };

        public JObject Data { get; set; }

        public Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        public async Task<Tuple<List<Channel>, List<Programme>>> Process()
        {
            ProgressUpdater?.Item1.Report(0);

            var hours = double.TryParse(ProviderParts.Setting(Data, "Cache Hours", "6"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 6;
            var address = ProviderParts.Setting(Data, "Playlist URL", string.Empty);
            var playlist = M3UParser.Parse(await ProviderParts.CachedList(address, hours).ConfigureAwait(false));
            var channels = ProviderParts.FromPlaylist(playlist);

            TvCore.LogInfo($"[{Title}] Loaded {playlist.Entries.Count} playlist entries");

            ProgressUpdater?.Item1.Report(100);

            var guide = await ProviderParts.XmltvGuide(ProviderParts.Setting(Data, "Guide URL", playlist.GuideUrls.FirstOrDefault()), hours, ProgressUpdater?.Item2).ConfigureAwait(false);

            return new Tuple<List<Channel>, List<Programme>>(channels, guide);
        }
    }
}
