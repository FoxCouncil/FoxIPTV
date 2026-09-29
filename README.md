# ═════•°• FoxIPTV •°•═════
The OSS FFmpeg based IPTV client, for Windows, macOS and Linux.

# ═════•°• The Why
     ____   _____  ____     _    _   _  ____   _____ 
    | __ ) | ____|/ ___|   / \  | | | |/ ___| | ____|
    |  _ \ |  _| | |      / _ \ | | | |\___ \ |  _|  
    | |_) || |___| |___  / ___ \| |_| | ___) || |___ 
    |____/ |_____|\____|/_/   \_\\___/ |____/ |_____|

# Versions •°•═════
## Version 3.0 (alpha)
- Runs on Windows, macOS and Linux
- Provider picker: Pluto TV, Plex, Roku, IPTV.org, Free-TV, M3U
- Program Guide as one scrollable surface
- Channel Editor with favourites per provider
- Stream tags, "(Ad)" readout during ad breaks, playback trace in the status bar
- Follows the system light or dark theme

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
| Pluto TV | Live channels with guide data | A region |
| Plex | Live channels with guide data | A region |
| Roku | Live channels with guide data | A region |
| IPTV.org | ~15,000 public live channels | Nothing |
| Free-TV | Live channels with guide data | A region |
| M3U Playlist | Any M3U/M3U8 URL, guide auto-detected from the playlist header or given by hand | The URL |

**Switch Provider...** in the menu takes you back to the picker.

# ═════•°• Building
Needs the .NET 10 SDK. `dotnet build src/FoxIPTV/FoxIPTV.csproj` builds the app, `dotnet test tests/FoxIPTV.Tests` runs the tests.

The player needs a small LGPL build of FFmpeg, made once into `native/<runtime>`. Run `build/ffmpeg.sh linux-x64` on Ubuntu 22.04, so the result also runs on 22.04 and newer, or `build/ffmpeg.sh osx-arm64` on macOS. On Windows, build it in a container from the repository root: `docker run --rm -v "${PWD}:/src" -w /src ubuntu:24.04 bash build/ffmpeg.sh win-x64` (PowerShell). The app build copies the files next to the app; without them the app starts but can't play.

# ═════•°• Roadmap
- IR Remote Support
- Chromecast'ing
- Hot Key Configuration
- Channel Favorites Only Navigation

# How To Help •°•═════
- [Donate (API Documentation/Account Details) For More IPTV Providers](https://forms.gle/yRH4HPUC5AqyUxYKA)
- Submit a Pull Request
