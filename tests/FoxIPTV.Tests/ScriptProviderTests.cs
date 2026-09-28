// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using FoxIPTV.Classes;
    using FoxIPTV.Services;
    using FoxIPTV.Services.Scripting;
    using Newtonsoft.Json.Linq;

    public class ScriptProviderTests
    {
        private const string Script = @"
var plugin = {
    id: 'Test Source', title: 'Test', description: 'For tests', version: '2',
    capabilities: ['live', 'library'],
    fields: [ { key: 'Token', kind: 'password' }, { key: 'Region', kind: 'choice', choices: ['us', 'gb'], default: 'us', required: false } ]
};
function authenticate(data) { return data.Token === 'ok'; }
function channels(progress) {
    progress(50);
    return [
        { index: 5, id: 'a', name: 'Alpha', group: 'News', logo: 'http://x/a.png', stream: 'http://x/a.m3u8' },
        { index: 5, id: 'b', name: 'Bravo', stream: 'http://x/b.m3u8' },
        { name: 'No stream' },
        { id: 'c', name: 'Charlie', url: 'http://x/c.m3u8' }
    ];
}
function guide(progress) {
    return [
        { channel: 'a', start: '2026-09-25T18:00:00Z', stop: '2026-09-25T19:00:00Z', title: 'News', desc: 'Tonight' },
        { channel: 'a', start: 1790362800, stop: 1790366400, title: 'Unix' },
        { channel: 'a', start: '2026-09-25T20:00:00Z', stop: '2026-09-25T19:00:00Z', title: 'Backwards' }
    ];
}
function resolve(item) {
    return [ 'http://x/plain.mp4', { name: 'HD', url: 'https://x/hd.m3u8', headers: { Referer: 'https://x/' } }, { url: 'javascript:alert(1)' } ];
}
";

        private static ScriptProvider Load()
        {
            var provider = new ScriptProvider(Script, "test", false)
            {
                ProgressUpdater = Tuple.Create<IProgress<int>, IProgress<int>>(new Progress<int>(), new Progress<int>())
            };

            return provider;
        }

        [Fact]
        public void Metadata_IsRead()
        {
            var provider = Load();

            Assert.Equal("test_source", provider.Id);
            Assert.Equal("2", provider.Version);
            Assert.Equal(ProviderCapabilities.LiveTv | ProviderCapabilities.Library, provider.Capabilities);
            Assert.Equal(ProviderFieldKind.Password, provider.Fields[0].Kind);
            Assert.False(provider.Fields[1].Required);
            Assert.Equal("us", provider.GetSetting("Region"));
        }

        [Fact]
        public async Task Authenticate_NeedsRequiredFieldsThenAsksTheScript()
        {
            var provider = Load();

            provider.Data = new JObject();

            Assert.False(await provider.IsAuthenticated());

            provider.Data = new JObject { ["Token"] = "wrong" };

            Assert.False(await provider.IsAuthenticated());

            provider.Data = new JObject { ["Token"] = "ok" };

            Assert.True(await provider.IsAuthenticated());
        }

        [Fact]
        public async Task Process_MapsChannelsAndGuide()
        {
            var provider = Load();

            var (channels, guide) = await provider.Process();

            Assert.Equal(new[] { "Bravo", "Charlie", "Alpha" }, channels.Select(x => x.Name));
            Assert.Equal(new uint[] { 1, 2, 5 }, channels.Select(x => x.Index));
            Assert.Equal("Uncategorized", channels[0].Group);
            Assert.Equal("News", channels[2].Group);

            Assert.Equal(2, guide.Count);
            Assert.Equal("Tonight", guide[0].Description);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790362800), guide[1].Start);
        }

        [Fact]
        public async Task Resolve_KeepsStreamsAndDropsOtherLinks()
        {
            var provider = Load();

            var sources = await provider.Resolve(new LibraryItem { Id = "m", Kind = LibraryItemKind.Movie, Title = "Movie" });

            Assert.Equal(2, sources.Count);
            Assert.Equal("Source 1", sources[0].Name);
            Assert.Equal("HD", sources[1].Name);
            Assert.Equal("https://x/", sources[1].Headers["Referer"]);
        }

        [Fact]
        public async Task Tune_GivesAFreshAddressOrNothing()
        {
            const string tuning = @"
var plugin = { id: 'tuner', title: 'Tuner', description: 'For tests', version: '1', capabilities: ['live'], fields: [] };
var calls = 0;
function channels(progress) { return [ { index: 1, id: 'a', name: 'Alpha', stream: 'http://x/a.m3u8' } ]; }
function tune(channel) {
    calls++;
    if (channel.id === 'a') { return 'https://x/fresh.m3u8?for=' + channel.name + '&call=' + calls; }
    if (channel.id === 'bad') { return 'javascript:alert(1)'; }
    return null;
}
";

            var provider = new ScriptProvider(tuning, "tuner", false);

            Assert.True(provider.CanTune);
            Assert.False(Load().CanTune);

            Assert.Equal("https://x/fresh.m3u8?for=Alpha&call=1", (await provider.Tune(new Channel { Id = "a", Name = "Alpha", Stream = new Uri("http://x/a.m3u8") })).ToString());
            Assert.Equal("https://x/fresh.m3u8?for=Alpha&call=2", (await provider.Tune(new Channel { Id = "a", Name = "Alpha" })).ToString());
            Assert.Null(await provider.Tune(new Channel { Id = "bad", Name = "Bad" }));
            Assert.Null(await provider.Tune(new Channel { Id = "b", Name = "Bravo" }));
        }
    }
}
