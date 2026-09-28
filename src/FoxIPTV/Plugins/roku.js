var plugin = {
    id: "roku",
    title: "Roku",
    version: "1",
    capabilities: ["live"],
    fields: []
};

var LIST = "https://raw.githubusercontent.com/BuddyChewChew/app-m3u-generator/main/playlists/roku_all.m3u";
var CACHE = { cache: 6 };

var playlist = null;

function region() {
    return (host.setting("Region", "all") || "all").toLowerCase().trim();
}

function load() {
    if (!playlist) {
        var url = LIST.replace("{region}", region());
        host.log("Fetching " + url);
        playlist = host.m3u.fetch(url, CACHE);
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

function pickGuide(urls) {
    if (urls.length === 0) {
        return null;
    }
    if (urls.length === 1) {
        return urls[0];
    }
    var tag = "_" + region().toUpperCase();
    for (var i = 0; i < urls.length; i++) {
        if (urls[i].indexOf(tag + "1.") >= 0 || urls[i].indexOf(tag + ".") >= 0) {
            return urls[i];
        }
    }
    return null;
}

function guide(progress) {
    var url = pickGuide(load().guideUrls);
    if (!url) {
        host.log("No guide available for this region");
        return [];
    }
    host.log("Fetching guide " + url);
    return host.xmltv.fetch(url, CACHE, progress);
}
