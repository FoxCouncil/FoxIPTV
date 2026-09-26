// FoxIPTV plugin: any M3U / M3U8 playlist, with an optional XMLTV guide.
//
// A plugin is a plain JavaScript file. It declares a global `plugin` object and defines
// some of the functions FoxIPTV knows about. This one is a live TV provider, so it defines
// channels() and guide(). An on-demand library provider defines categories(), browse(), search(),
// details(), episodes() and resolve() instead; see the README for the shapes.
//
// The `host` object is your bridge to FoxIPTV:
//   host.setting(key, default)        the value the user typed for one of your fields
//   host.fetch(url, options)          GET a URL as text; options = { headers: {}, cache: hours, cacheKey: "name" }
//   host.fetchJson(url, options)      same, parsed as JSON
//   host.m3u.fetch(url, options)      GET and parse a playlist -> { guideUrls: [], entries: [ { index, id, name, group, logo, url, attributes, options } ] }
//   host.m3u.parse(text)              parse playlist text you already have
//   host.xmltv.fetch(url, options, progress)  GET and parse an XMLTV guide (gzip is fine) -> return it straight from guide()
//   host.xmltv.parse(text, progress)  parse guide text you already have
//   host.log(message)                 write to the FoxIPTV log
//   host.userAgent                    the browser user agent FoxIPTV sends

var plugin = {
    id: "m3u",
    title: "M3U Playlist",
    description: "Any M3U or M3U8 playlist URL, with an optional XMLTV guide; the guide is auto-detected from the playlist header when it declares one",
    version: "1",
    capabilities: ["live"],
    fields: [
        { key: "Playlist URL", kind: "url" },
        { key: "Guide URL", kind: "url", required: false },
        { key: "Cache Hours", kind: "text", default: "6", required: false }
    ]
};

var playlist = null;

function cacheHours() {
    var hours = parseFloat(host.setting("Cache Hours", "6"));
    return isNaN(hours) ? 6 : hours;
}

function load() {
    if (!playlist) {
        playlist = host.m3u.fetch(host.setting("Playlist URL"), { cache: cacheHours() });
        host.log("Loaded " + playlist.entries.length + " playlist entries");
    }
    return playlist;
}

// Return an array of { index, id, name, group, logo, stream }. `index` is the channel number;
// duplicates or missing numbers are renumbered by FoxIPTV.
function channels(progress) {
    var list = load();
    var out = [];
    for (var i = 0; i < list.entries.length; i++) {
        var e = list.entries[i];
        out.push({ index: e.index, id: e.id, name: e.name, group: e.group, logo: e.logo, stream: e.url });
        if (i % 500 === 0) {
            progress(i * 100 / list.entries.length);
        }
    }
    return out;
}

// Return either a native guide from host.xmltv.* or an array of
// { channel, start, stop, title, description } where start/stop are ISO strings, epoch values, or XMLTV timestamps.
function guide(progress) {
    var url = host.setting("Guide URL", "");
    if (!url) {
        var urls = load().guideUrls;
        if (urls.length > 0) {
            url = urls[0];
        }
    }
    if (!url) {
        return [];
    }
    return host.xmltv.fetch(url, { cache: cacheHours() }, progress);
}
