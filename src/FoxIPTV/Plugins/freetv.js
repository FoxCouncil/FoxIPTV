//
//

var plugin = {
    id: "freetv",
    title: "Free TV Playlists",
    description: "Pluto TV, Samsung TV Plus, Plex, Roku and Free-TV public playlists with guide data, no account needed",
    version: "1",
    capabilities: ["live"],
    fields: [
        { key: "Source", kind: "choice", choices: ["Pluto TV", "Samsung TV Plus", "Plex", "Roku", "Free-TV"], default: "Samsung TV Plus" },
        { key: "Region", kind: "choice", choices: ["us", "ca", "gb", "au", "nz", "de", "fr", "es", "it", "at", "ch", "dk", "no", "se", "in", "kr", "mx", "br", "ar", "cl", "all"], default: "us" }
    ]
};

var GENERATED = "https://raw.githubusercontent.com/BuddyChewChew/app-m3u-generator/main/playlists/";

var SOURCES = {
    "Pluto TV":        { url: function (r) { return GENERATED + "plutotv_" + r + ".m3u"; },        regions: ["all", "ar", "br", "ca", "cl", "de", "dk", "es", "fr", "gb", "it", "mx", "no", "se", "us"] },
    "Samsung TV Plus": { url: function (r) { return GENERATED + "samsungtvplus_" + r + ".m3u"; },  regions: ["all", "at", "ca", "ch", "de", "es", "fr", "gb", "in", "it", "kr", "us"] },
    "Plex":            { url: function (r) { return GENERATED + "plex_" + r + ".m3u"; },           regions: ["all", "au", "ca", "es", "fr", "gb", "mx", "nz", "us"] },
    "Roku":            { url: function (r) { return GENERATED + "roku_all.m3u"; },                 regions: ["all"] },
    "Free-TV":         { url: function (r) { return "https://raw.githubusercontent.com/Free-TV/IPTV/master/playlist.m3u8"; }, regions: ["all"] }
};

function currentSource() {
    return SOURCES[host.setting("Source", "Samsung TV Plus")] || SOURCES["Samsung TV Plus"];
}

var playlist = null;
var CACHE = { cache: 6 };

function region() {
    var r = (host.setting("Region", "us") || "us").toLowerCase().trim();
    var source = currentSource();
    if (source.regions && source.regions.indexOf(r) < 0) {
        host.log("Region '" + r + "' not offered by this source, using '" + source.regions[source.regions.length - 1] + "'");
        r = source.regions[source.regions.length - 1];
    }
    return r;
}

function load() {
    if (!playlist) {
        var source = currentSource();
        var url = source.url(region());
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
        host.log("No guide available for this source and region");
        return [];
    }
    host.log("Fetching guide " + url);
    return host.xmltv.fetch(url, CACHE, progress);
}
