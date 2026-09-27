//
//

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
