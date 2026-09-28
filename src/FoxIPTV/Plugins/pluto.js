var plugin = {
    id: "pluto",
    title: "Pluto TV",
    version: "1",
    capabilities: ["live"],
    fields: []
};

var BOOT = "https://boot.pluto.tv/v4/start";
var GUIDE = "https://service-channels.clusters.pluto.tv/v2/guide/";
var CHANNEL_ADDRESS = "https://service-stitcher.clusters.pluto.tv/v2/stitch/hls/channel/";
var WEB = { Origin: "https://pluto.tv", Referer: "https://pluto.tv/" };
var RENEW_SECONDS = 28800;
var GUIDE_MINUTES = 1440;
var GUIDE_BATCH = 100;

var session = null;
var client = null;
var channelIds = [];

function clientId() {
    if (!client) {
        var hex = host.md5(Math.random() + ":" + Date.now());
        client = hex.substr(0, 8) + "-" + hex.substr(8, 4) + "-4" + hex.substr(13, 3) + "-8" + hex.substr(17, 3) + "-" + hex.substr(20, 12);
    }
    return client;
}

function boot() {
    var now = Date.now();
    if (session && now < session.renewAt) {
        return session;
    }
    var query = [
        "appName=web", "appVersion=9.1.2", "deviceVersion=128.0.0", "deviceModel=web", "deviceMake=chrome", "deviceType=web",
        "clientID=" + clientId(), "clientModelNumber=1.0.0", "serverSideAds=false", "drmCapabilities=", "blockingMode=",
        "notificationVersion=1", "appLaunchCount=0", "lastAppLaunchDate="
    ].join("&");
    var started = host.fetchJson(BOOT + "?" + query, { headers: WEB });
    session = {
        token: started.sessionToken,
        stitcher: started.servers.stitcher,
        params: started.stitcherParams,
        renewAt: now + Math.min(started.refreshInSec || RENEW_SECONDS, RENEW_SECONDS) * 1000
    };
    host.log("Pluto TV session started, region " + (started.session ? started.session.activeRegion : "unknown"));
    return session;
}

function headers() {
    return { Origin: WEB.Origin, Referer: WEB.Referer, Authorization: "Bearer " + boot().token };
}

function logo(channel) {
    var images = channel.images || [];
    for (var i = 0; i < images.length; i++) {
        if (images[i].type === "colorLogoPNG") {
            return images[i].url;
        }
    }
    return images.length > 0 ? images[0].url : null;
}

function channels(progress) {
    var list = host.fetchJson(GUIDE + "channels?channelIds=&offset=0&limit=1000&sort=number%3Aasc", { headers: headers() }).data || [];
    var categories = host.fetchJson(GUIDE + "categories", { headers: headers() }).data || [];
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
    channelIds = [];
    for (var i = 0; i < list.length; i++) {
        var channel = list[i];
        out.push({ index: channel.number, id: channel.id, name: channel.name, group: groups[channel.id] || "Pluto TV", logo: logo(channel), stream: CHANNEL_ADDRESS + channel.id + "/master.m3u8" });
        channelIds.push(channel.id);
        if (i % 100 === 0) {
            progress(i * 100 / list.length);
        }
    }
    host.log("Pluto TV lists " + out.length + " channels");
    return out;
}

function guide(progress) {
    var start = new Date();
    start.setUTCMinutes(start.getUTCMinutes() < 30 ? 0 : 30, 0, 0);
    var out = [];
    for (var i = 0; i < channelIds.length; i += GUIDE_BATCH) {
        var batch = channelIds.slice(i, i + GUIDE_BATCH).join(",");
        var data = host.fetchJson(GUIDE + "timelines?start=" + start.toISOString() + "&channelIds=" + batch + "&duration=" + GUIDE_MINUTES, { headers: headers() }).data || [];
        for (var c = 0; c < data.length; c++) {
            var timelines = data[c].timelines || [];
            for (var t = 0; t < timelines.length; t++) {
                var entry = timelines[t];
                out.push({ channel: data[c].channelId, start: entry.start, stop: entry.stop, title: entry.title, description: entry.episode ? entry.episode.description : "" });
            }
        }
        progress(Math.min(100, (i + GUIDE_BATCH) * 100 / channelIds.length));
    }
    return out;
}

function tune(channel) {
    if (!channel || !channel.id) {
        return null;
    }
    var current = boot();
    return current.stitcher + "/v2/stitch/hls/channel/" + channel.id + "/master.m3u8?" + current.params + "&jwt=" + current.token + "&masterJWTPassthrough=true";
}
