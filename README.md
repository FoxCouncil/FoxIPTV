# ═════•°• FoxIPTV •°•═════
The OSS LibVLC based IPTV client, for Windows, macOS and Linux.

# ═════•°• The Why
     ____   _____  ____     _    _   _  ____   _____ 
    | __ ) | ____|/ ___|   / \  | | | |/ ___| | ____|
    |  _ \ |  _| | |      / _ \ | | | |\___ \ |  _|  
    | |_) || |___| |___  / ___ \| |_| | ___) || |___ 
    |____/ |_____|\____|/_/   \_\\___/ |____/ |_____|

# Versions •°•═════
## Version 1.0 - Huskytail
- Supports Single Server Vendor
- Program Guide
- Channel Editor
- Stereo Mode Control
- Transparency
- Hot Keys

# ═════•°• Providers
Pick one at start up. Details are remembered per provider when you tick "Remember these details": encrypted to your Windows account on Windows, and with a key only your user account can read on macOS and Linux.

| Provider | Gives you | Needs |
|---|---|---|
| Xtream IPTV | Live channels + guide | Panel URL, username, password |
| IPTV.org | ~15,000 public live channels | Nothing |
| Free TV Playlists | Pluto TV, Samsung TV Plus, Plex, Roku, Tubi, Free-TV or iptv-org, with guide data | A source and a region |
| M3U Playlist | Any M3U/M3U8 URL, guide auto-detected from the playlist header or given by hand | The URL |

Providers that offer a library open the **Library** window (right click menu) where you browse categories, search, pick seasons and episodes, and choose a source. Every source is a direct stream URL that LibVLC plays; there is no browser in FoxIPTV. **Switch Provider...** in the menu takes you back to the picker.

# ═════•°• Writing a plugin
Free TV Playlists and M3U Playlist are JavaScript files; Xtream and IPTV.org are C#. Drop your own `.js` into the `FoxIPTV/plugins` folder of your application data (`%APPDATA%` on Windows, `~/.config` on Linux and macOS; **Open plugins folder** in the picker takes you there) and it appears in the picker on the next start; a file with the same name as a built-in replaces it. The built-ins are copied to `plugins\examples` for reference, and plugins that fail to load are listed in the picker with the error.

A plugin declares a `plugin` object and defines the functions it supports:

```js
var plugin = {
    id: "mysource", title: "My Source", description: "One line for the picker", version: "1",
    capabilities: ["live", "library"],           // one or both
    fields: [                                     // asked for in the picker, read back with host.setting(key)
        { key: "Server URL", kind: "url" },       // kinds: text, password, url, choice
        { key: "Region", kind: "choice", choices: ["us", "gb"], default: "us", required: false }
    ]
};

// live
function authenticate(data) { return true; }              // optional
function channels(progress) { return [ { index, id, name, group, logo, stream } ]; }
function guide(progress) { return host.xmltv.fetch(url, { cache: 6 }, progress); }   // or an array of { channel, start, stop, title, description }

// library
function categories() { return [ { id, name } ]; }
function browse(categoryId, page) { return { items: [ item ], page, totalPages }; }
function search(query, page) { /* same shape */ }
function details(id, kind) { /* item, with seasons: [ { number, name, episodeCount } ] for series */ }
function episodes(seriesId, season) { return [ item ]; }
function resolve(item) { return [ { name, url, headers } ]; }   // url must be something LibVLC can open: HLS, MP4, MPEG-TS, RTSP, ...
```

An item is `{ id, kind: "movie" | "series" | "episode", title, subtitle, year, overview, poster, backdrop, rating, duration, seriesId, season, episode, seasons, extra }`.

The `host` object is the bridge back to FoxIPTV:

| Call | Does |
|---|---|
| `host.fetch(url, opts)` / `host.fetchJson(url, opts)` | GET with a real browser user agent; `opts = { headers: {}, cache: hours, cacheKey: "name" }`; gzip bodies are inflated |
| `host.m3u.fetch(url, opts)` / `host.m3u.parse(text)` | `{ guideUrls: [], entries: [ { index, id, name, group, logo, url, attributes, options } ] }` |
| `host.xmltv.fetch(url, opts, progress)` / `host.xmltv.parse(text, progress)` | A native guide you return straight from `guide()`; the text never enters the script engine |
| `host.setting(key, default)` | What the user typed for a field |
| `host.log(message)` | Writes to `FoxIPTV.log` |
| `host.md5(text)`, `host.userAgent` | Handy bits |

Scripts run off the UI thread and one call at a time, so plain blocking code is fine. A Debug build can exercise a plugin without the UI: `FoxIPTV --test-provider <id> "Field=Value" --search text` writes a report to `selftest-<id>.txt` in the temp folder's `FoxIPTV` directory.

# ═════•°• Building
Needs the .NET 10 SDK. `dotnet build src/FoxIPTV/FoxIPTV.csproj` builds the app, `dotnet test tests/FoxIPTV.Tests` runs the tests. On Linux, install VLC from your package manager (`sudo apt install vlc libvlc-dev`); on macOS, install VLC.app.

# ═════•°• Roadmap
- IR Remote Support
- Chromecast'ing
- Hot Key Configuration
- Channel Favorites Only Navigation

# How To Help •°•═════
- [Donate (API Documentation/Account Details) For More IPTV Providers](https://forms.gle/yRH4HPUC5AqyUxYKA)
- Submit a Pull Request
