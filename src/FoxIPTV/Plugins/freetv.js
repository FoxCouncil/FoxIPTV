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
    "Pluto TV":        { pluto: true },
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

var PLUTO_BOOT = "https://boot.pluto.tv/v4/start";
var PLUTO_GUIDE = "https://service-channels.clusters.pluto.tv/v2/guide/";
var PLUTO_WEB = { Origin: "https://pluto.tv", Referer: "https://pluto.tv/" };
var PLUTO_RENEW_SECONDS = 28800;
var PLUTO_GUIDE_MINUTES = 1440;
var PLUTO_GUIDE_BATCH = 100;

var plutoSession = null;
var plutoClient = null;
var plutoChannelIds = [];

function plutoClientId() {
    if (!plutoClient) {
        var hex = host.md5(Math.random() + ":" + Date.now());
        plutoClient = hex.substr(0, 8) + "-" + hex.substr(8, 4) + "-4" + hex.substr(13, 3) + "-8" + hex.substr(17, 3) + "-" + hex.substr(20, 12);
    }
    return plutoClient;
}

function plutoBoot() {
    var now = Date.now();
    if (plutoSession && now < plutoSession.renewAt) {
        return plutoSession;
    }
    var query = [
        "appName=web", "appVersion=9.1.2", "deviceVersion=128.0.0", "deviceModel=web", "deviceMake=chrome", "deviceType=web",
        "clientID=" + plutoClientId(), "clientModelNumber=1.0.0", "serverSideAds=false", "drmCapabilities=", "blockingMode=",
        "notificationVersion=1", "appLaunchCount=0", "lastAppLaunchDate="
    ].join("&");
    var boot = host.fetchJson(PLUTO_BOOT + "?" + query, { headers: PLUTO_WEB });
    plutoSession = {
        token: boot.sessionToken,
        stitcher: boot.servers.stitcher,
        params: boot.stitcherParams,
        renewAt: now + Math.min(boot.refreshInSec || PLUTO_RENEW_SECONDS, PLUTO_RENEW_SECONDS) * 1000
    };
    host.log("Pluto TV session started, region " + (boot.session ? boot.session.activeRegion : "unknown"));
    return plutoSession;
}

function plutoHeaders() {
    return { Origin: PLUTO_WEB.Origin, Referer: PLUTO_WEB.Referer, Authorization: "Bearer " + plutoBoot().token };
}

function plutoStream(id) {
    var session = plutoBoot();
    return session.stitcher + "/v2/stitch/hls/channel/" + id + "/master.m3u8?" + session.params + "&jwt=" + session.token + "&masterJWTPassthrough=true";
}

function plutoLogo(channel) {
    var images = channel.images || [];
    for (var i = 0; i < images.length; i++) {
        if (images[i].type === "colorLogoPNG") {
            return images[i].url;
        }
    }
    return images.length > 0 ? images[0].url : null;
}

function plutoChannels(progress) {
    var list = host.fetchJson(PLUTO_GUIDE + "channels?channelIds=&offset=0&limit=1000&sort=number%3Aasc", { headers: plutoHeaders() }).data || [];
    var categories = host.fetchJson(PLUTO_GUIDE + "categories", { headers: plutoHeaders() }).data || [];
    var groups = {};
    for (var c = 0; c < categories.length; c++) {
        var ids = categories[c].channelIDs || [];
        for (var k = 0; k < ids.length; k++) {
            if (!groups[ids[k]]) {
                groups[ids[k]] = categories[c].name;
            }
        }
    }
    var out = [];
    plutoChannelIds = [];
    for (var i = 0; i < list.length; i++) {
        var channel = list[i];
        out.push({ index: channel.number, id: channel.id, name: channel.name, group: groups[channel.id] || "Pluto TV", logo: plutoLogo(channel), stream: plutoStream(channel.id) });
        plutoChannelIds.push(channel.id);
        if (i % 100 === 0) {
            progress(i * 100 / list.length);
        }
    }
    host.log("Pluto TV lists " + out.length + " channels");
    return out;
}

function plutoGuide(progress) {
    var start = new Date();
    start.setUTCMinutes(start.getUTCMinutes() < 30 ? 0 : 30, 0, 0);
    var out = [];
    for (var i = 0; i < plutoChannelIds.length; i += PLUTO_GUIDE_BATCH) {
        var batch = plutoChannelIds.slice(i, i + PLUTO_GUIDE_BATCH).join(",");
        var data = host.fetchJson(PLUTO_GUIDE + "timelines?start=" + start.toISOString() + "&channelIds=" + batch + "&duration=" + PLUTO_GUIDE_MINUTES, { headers: plutoHeaders() }).data || [];
        for (var c = 0; c < data.length; c++) {
            var timelines = data[c].timelines || [];
            for (var t = 0; t < timelines.length; t++) {
                var entry = timelines[t];
                out.push({ channel: data[c].channelId, start: entry.start, stop: entry.stop, title: entry.title, description: entry.episode ? entry.episode.description : "" });
            }
        }
        progress(Math.min(100, (i + PLUTO_GUIDE_BATCH) * 100 / plutoChannelIds.length));
    }
    return out;
}

function tune(channel) {
    if (!currentSource().pluto || !channel || !channel.id) {
        return null;
    }
    return plutoStream(channel.id);
}

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
    if (currentSource().pluto) {
        return plutoChannels(progress);
    }
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
    if (currentSource().pluto) {
        return plutoGuide(progress);
    }
    var url = pickGuide(load().guideUrls);
    if (!url) {
        host.log("No guide available for this source and region");
        return [];
    }
    host.log("Fetching guide " + url);
    return host.xmltv.fetch(url, CACHE, progress);
}
