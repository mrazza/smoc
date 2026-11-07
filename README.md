
<div align="center">

![logo](logo.png)

# SMoC
### Steaming Music on Console

![License](https://img.shields.io/github/license/mrazza/smoc?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet)
![Status](https://img.shields.io/badge/status-active_development-green?style=flat-square)
[![CI](https://github.com/mrazza/smoc/actions/workflows/dotnet.yml/badge.svg)](https://github.com/mrazza/smoc/actions/workflows/dotnet.yml)

A terminal-based music player (TUI) for streaming services, currently supporting **Subsonic**-compatible APIs, **YouTube Music**, and **SoundCloud**. Spotify, Apple Music, and other services are _future features_.

[Features](#-features) • [Installation](#-installation) • [Configuration and Setup](#%EF%B8%8F-configuration-and-setup) • [Usage](#-usage)

![screenshot animated](smoc.gif)

</div>

---

> [!NOTE]
> This project is not supported or endorsed by any streaming music service or major company. It is a hobby project and has no affiliation with Spotify, Apple, Google, or YouTube.
> The same warnings and disclaimers for the well-known [yt-dlp](https://github.com/yt-dlp/yt-dlp) project apply here.

## 🌟 Features

- **TUI Interface**: Built with [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) for a rich console experience.
- **Cross-Platform Audio**: Local playback powered by [SoundFlow](https://github.com/LSXPrime/SoundFlow).
- **Google Cast Support**: Stream audio directly to Chromecast devices, Nest smart speakers, and Cast-enabled receivers on your local network using on-demand mDNS discovery and a built-in local streaming proxy.
- **YouTube Music Integration**: Powered by [YouTubeMusicAPI](https://github.com/IcySnex/YouTubeMusicAPI). Search, stream, and manage your library.
- **Subsonic API Integration**: Streaming client for any Subsonic-compatible server. For instance, [Navidrome](https://www.navidrome.org/), [Airsonic Advanced](https://github.com/airsonic-advanced/airsonic-advanced), [Gonic](https://github.com/sentriz/gonic), or [Madsonic](https://www.madsonic.org/).
- **SoundCloud Integration**: Search and stream tracks, artists, and playlists from SoundCloud.
- **Session Persistence**: Automatically saves and restores your playback queue, active track, playback position, and volume across restarts.
- **Visuals**: Displays album art using Sixel graphics (requires a compatible terminal) and a beautiful real-time audio frequency spectrum visualizer.

### Feature Progress
While most basic functionality is available and SMoC is totally usable, there's a lot left to do.

<details>
<summary><strong>Feature Plan</strong> (Click to expand)</summary>
  
- [x] UI
  - [x] Search Support
  - [x] Command Bar (with Command History & Tab Completion)
  - [x] Incremental Table Filtering (`/`)
  - [x] Song Table
  - [x] Now Playing Bar
  - [x] Album Art (Sixel)
  - [x] Status Bar
  - [x] Visualizations
  - [x] Now Playing Screen
- [ ] Playback
  - [x] Play
  - [x] Pause
  - [x] Stop
  - [x] Skip
  - [x] Previous
  - [x] Start Over
  - [ ] Repeat Track
  - [x] Volume (0–200% with Loudness Normalization)
  - [x] Playback Device Selection (Local & Google Cast)
  - [x] Gapless Playback (Next-Track Preloading)
  - [x] Session State Persistence
- [ ] Queue
  - [x] Add to End of Queue
  - [x] Queue Next (after current song)
  - [ ] Remove
  - [x] Advance to Next at End of Song
  - [ ] Repeat Queue
  - [ ] Shuffle
- [x] Search
  - [x] Artist
  - [x] Track
  - [x] Playlist
  - [x] URL
- [ ] YouTube Music
  - [x] Authentication
  - [x] POToken
  - [x] VisitorData
  - [x] Metadata
  - [x] Searching
  - [x] Play Audio Stream
  - [x] Album Art
  - [x] Caching
  - [x] History Tracking
  - [ ] Like
  - [ ] Dislike
- [ ] Subsonic API-compatible Servers
  - [x] Authentication
  - [x] Metadata
  - [x] Searching
  - [x] Play Audio Stream
  - [x] Album Art
  - [x] Caching
  - [x] History Tracking
  - [ ] Like
  - [ ] Dislike
- [ ] SoundCloud
  - [x] Authentication
  - [x] Metadata
  - [x] Searching
  - [x] Play Audio Stream
  - [x] Album Art
  - [x] Caching
  - [x] History Tracking
  - [ ] Like
  - [ ] Dislike
- [ ] Apple Music ([gamdl](https://github.com/glomatico/gamdl) as a reference)
  - [ ] Authentication
  - [ ] Play Audio Stream
  - [ ] Metadata
  - [ ] Searching
  - [ ] Play Audio Stream
  - [ ] Album Art
  - [ ] Caching
  - [ ] History Tracking
  - [ ] Like
  - [ ] Dislike
- [ ] Spotify ([librespot](https://github.com/librespot-org/librespot) as a reference)
  - [ ] Authentication
  - [ ] Play Audio Stream
  - [ ] Metadata
  - [ ] Searching
  - [ ] Play Audio Stream
  - [ ] Album Art
  - [ ] Caching
  - [ ] History Tracking
  - [ ] Like
  - [ ] Dislike
  
</details>

## 🚀 Installation

Once installed some setup is required to use SMoC. See the [Configuration and Setup](#%EF%B8%8F-configuration-and-setup) section for more information.

### Binary Release (Windows/Linux/MacOS)
Current release is [v0.5.4](https://github.com/mrazza/smoc/releases/tag/v0.5.4).

Available for [Windows x64](https://github.com/mrazza/smoc/releases/download/v0.5.4/smoc-v0.5.4-win-x64-bin.zip), [Linux x64](https://github.com/mrazza/smoc/releases/download/v0.5.4/smoc-v0.5.4-linux-x64-bin.tar.gz), and [MacOS ARM64](https://github.com/mrazza/smoc/releases/download/v0.5.4/smoc-v0.5.4-mac-arm64-bin.zip).

### Gentoo via Portage
SMoC is available via [my overlay](https://github.com/mrazza/razza-overlay). To install, add my overlay and emerge it.
```
eselect repository add razza git https://github.com/mrazza/razza-overlay.git
emerge media-sound/smoc
```

### Building from Source

```bash
# Clone the repository
git clone https://github.com/mrazza/smoc.git
cd smoc

# Build and run
dotnet run --project smoc/smoc.csproj
```

## 🎮 Usage

SMoC operates with a Vim-style command bar. Press `:` to enter command mode (supports `Up`/`Down` command history and `Tab` command completion) or `/` to filter the active view incrementally.

### Navigation & Commands

| Command                  | Description                                                           |
| :----------------------- | :-------------------------------------------------------------------- |
| `:a`                     | Switch to **Artist** mode                                             |
| `:a/<artist>`            | Switch to **Artist** mode and search for `<artist>`                   |
| `:t`                     | Switch to **Track** mode                                              |
| `:t/<track>`             | Switch to **Track** mode and search for `<track>`                     |
| `:p`                     | Switch to **Playlist** mode                                           |
| `:p/<playlist>`          | Switch to **Playlist** mode and search for `<playlist>`               |
| `:likes`                 | Load your **Liked Songs** playlist                                    |
| `:url`                   | Switch to **Playlist** mode from URL                                  |
| `:url/<url>`             | Load songs from a specific streaming URL                              |
| `:pq`                    | View the **Playback Queue**                                           |
| `:np`                    | View **Now Playing** screen                                           |
| `:np-vis`                | Toggle real-time frequency visualizer in Now Playing                  |
| `:np-vis-fps/<1-60>`     | Set real-time frequency visualizer refresh rate in FPS                |
| `:output`                | List available audio output targets (local & Cast devices)            |
| `:output/<device>`       | Switch output to a Cast device (name/IP), or `:output/local`          |
| `:output/refresh`        | Perform an active on-demand mDNS scan for Cast devices                |
| `:v/<0-200>`             | Set volume percentage (0–200%, e.g., `:v/80` or `:v/150`)             |
| `:devices`               | List available audio playback devices (`*` marks the active device)   |
| `:device/<name-or-idx>`  | Switch active audio output device by name, substring, or index        |
| `/<query>`               | Incrementally filter the active table view (`Enter` keeps, `Esc` clears) |
| `:q`                     | **Quit** application (automatically saves session state)              |

### Audio Outputs & Casting

SMoC supports switching audio output on the fly without interrupting your listening session:

* **List outputs:** Enter `:output` to view all available local and networked Cast devices.
* **Cast to a device:** Type `:output/Living Room` (or `:output/192.168.1.50`). Playback immediately transfers to the Chromecast device and resumes at the current track and position.
* **Return to local audio:** Enter `:output/local` to switch back to local sound card playback.
* **Rescan network:** If a device was recently powered on, enter `:output/refresh` to force an active mDNS probe.
* **Tab-completion:** Typing `:output/` and pressing `Tab` automatically completes matching device names, `local`, and `refresh`.

### Playback Controls

| Hotkey               | Action                                                        |
| :------------------- | :------------------------------------------------------------ |
| `Space`              | Play / Pause                                                  |
| `Ctrl+Space`         | Stop                                                          |
| `,` (Comma)          | Previous Track (Restart if > 10s played)                      |
| `.` (Period)         | Next Track                                                    |
| `[`                  | Seek Backward 10s                                             |
| `]`                  | Seek Forward 10s                                              |
| `Up/Down/Left/Right` | Navigate Tables                                               |
| `h/j/k/l`            | Navigate Tables (Vim style)                                   |
| `Shift + Nav Key`    | Select multiple items                                         |
| `Tab`                | Switch active pane (or autocomplete command in `:` mode)      |
| `/`                  | Enter incremental filter mode for the active view             |
| `v` / `V` (Shift+v)  | Toggle real-time frequency visualizer (on Now Playing screen) |
| `Enter`              | Open Song Context Menu for selected item                      |

### Song Context Menu

The Song Context Menu provides options for playback and queue management. It appears when pressing `Enter` while a song is selected in any song table.

| Hotkey        | Action                   | Details                                                                                                          |
| :------------ | :----------------------- | :--------------------------------------------------------------------------------------------------------------- |
| `p`           | Play all from here       | Clears the playback queue and queues the track listing in its entirety. Then starts playback at the selected track. |
| `o`           | Play selection only      | Clears the playback queue and queues only the selected tracks. Then starts playback at the first selected track.  |
| `n`           | Queue next               | Adds the selected tracks to the playback queue after the current track.                                           |
| `q`           | Queue last               | Adds the selected tracks to the end of the playback queue.                                                       |
| `Up/Down/j/k` | Navigate menu            | Moves the selection through the menu items.                                                                      |
| `Enter`       | Execute selected action  | Executes the highlighted action and closes the menu.                                                             |
| `Esc`         | Close menu               | Dismisses the context menu without taking any action.                                                            |

## ⚙️ Configuration and Setup

SMoC stores configuration and authentication data in `~/.config/smoc/` (on Linux).

### YouTube Music (Default)

YouTube Music is the default streaming service in SMoC. Setting `ActiveService` to `YouTubeMusic` under `SmocConfiguration` is optional unless you need to switch back from another service.

#### Authentication

Unlike other services where credentials are saved directly in `config.json`, YouTube Music authenticates using session cookies and tokens.

> [!NOTE]
> You need a YouTube Music Premium subscription to play most content.

To access your YouTube Music account, you need to extract your cookies from YouTube Music (these steps are copied from [YouTube.js](https://ytjs.dev/guide/authentication.html)):

1. Open [YouTube Music](https://music.youtube.com) in your browser (Incognito recommended).
2. Open Developer Tools (`F12`) -> **Network** tab.
3. Find a `POST` request to `music.youtube.com`.
4. Copy the value of the `cookie` request header.
5. Save it to `~/.config/smoc/cookies.txt`.

> [!TIP]
> It is recommended to open an incognito window or separate browser profile when getting your cookie information to avoid other browser sessions from invalidating the tokens. Once cookies are retrieved, close the session _without_ logging out.

More recently, additional tokens beyond just cookies are often needed. POToken and Visitor Data are required in the `tokens.json` file in the same directory as your `cookies.txt`.

> [!TIP]
> Run `smoc --gentokens` after setting up the cookie to generate necessary PO Tokens and Visitor Data automatically.

### Subsonic

To use a Subsonic-compatible service (like Navidrome, Gonic, or Airsonic):
1. Ensure your server has the Subsonic API enabled.
2. Set `ActiveService` to `Subsonic` under `SmocConfiguration` in `config.json`.
3. Configure your server hostname, port, scheme, username, and password under `SubsonicConfig` in `config.json` (see below).

### SoundCloud

To use SoundCloud:
1. Set `ActiveService` to `SoundCloud` under `SmocConfiguration` in `config.json`.
2. Configure `ClientId` and `AuthToken` under `SoundCloudConfig` in `config.json` (see below).

### Config File
Create or edit `~/.config/smoc/config.json` to customize settings.

<details>
<summary><strong>Example Configuration</strong> (Click to expand)</summary>

```json
{
  "SmocConfiguration": {
    "LogLevel": "Information",
    "ActiveService": "YouTubeMusic",
    "SongCacheSizeBytes": 536870912,
    "SongCacheMaxElements": 0,
    "AlbumCoverCacheSizeBytes": 104857600,
    "AlbumCoverCacheMaxElements": 0,
    "VisualizerFps": 24,
    "EnableLoudnessNormalization": true,
    "LoudnessNormalizationMode": "Full",
    "AudioDevice": null,
    "DeduplicateArtistSingles": true
  },
  "SubsonicConfig": {
    "ServerScheme": "https",
    "ServerHost": "subsonic.example.com",
    "ServerPort": 443,
    "Username": "username",
    "Password": "password",
    "UseToken": true
  },
  "ListenHistoryConfig": {
    "Enabled": true,
    "MinimumPositionSeconds": 30,
    "MinimumFraction": 0.5
  },
  "SoundCloudConfig": {
    "ClientId": "your-client-id",
    "AuthToken": "your-auth-token"
  },
  "YouTubeMusicConfig": {
    "PlayerId": "your-player-id"
  }
}
```
</details>

#### Common Settings
| Section | Key | Type | Default | Description |
| :--- | :--- | :--- | :------ | :---------- |
| `SmocConfiguration` | `ActiveService` | `StreamingService` | `YouTubeMusic` | Active service (`YouTubeMusic`, `Subsonic`, `SoundCloud`) |
| `SmocConfiguration` | `LogLevel` | `LogLevel` | `Information` | Minimum log level (`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`) |
| `SmocConfiguration` | `SongCacheSizeBytes` | `long` | `536870912` | Max song cache size in bytes (0 = no limit, default 512MB) |
| `SmocConfiguration` | `SongCacheMaxElements` | `int` | `0` | Max songs to cache (0 = no limit) |
| `SmocConfiguration` | `AlbumCoverCacheSizeBytes` | `long` | `104857600` | Max album cover cache size in bytes (0 = no limit, default 100MB) |
| `SmocConfiguration` | `AlbumCoverCacheMaxElements` | `int` | `0` | Max album covers to cache (0 = no limit) |
| `SmocConfiguration` | `VisualizerFps` | `int` | `24` | Refresh rate for the audio frequency spectrum visualizer (FPS) |
| `SmocConfiguration` | `EnableLoudnessNormalization` | `bool` | `true` | Enable track loudness normalization |
| `SmocConfiguration` | `LoudnessNormalizationMode` | `LoudnessNormalizationMode` | `Full` | Loudness normalization mode (`Full`, `AttenuateOnly`) |
| `SmocConfiguration` | `AudioDevice` | `string` | `null` | Preferred audio output device name or index (`null` = system default) |
| `SmocConfiguration` | `DeduplicateArtistSingles` | `bool` | `true` | Filter out duplicate teaser singles already on an album in Artist mode |
| `SubsonicConfig` | `ServerScheme` | `string` | `http` | Subsonic API URI scheme (`http`, `https`) |
| `SubsonicConfig` | `ServerHost` | `string` | `null` | Subsonic server hostname |
| `SubsonicConfig` | `ServerPort` | `int` | `80` | Subsonic server port |
| `SubsonicConfig` | `Username` | `string` | `null` | Subsonic username |
| `SubsonicConfig` | `Password` | `string` | `null` | Subsonic password |
| `SubsonicConfig` | `UseToken` | `bool` | `true` | Use token auth instead of password |
| `ListenHistoryConfig` | `Enabled` | `bool` | `true` | Enable listen history tracking |
| `ListenHistoryConfig` | `MinimumPositionSeconds` | `int` | `30` | Min seconds for 'listened' status |
| `ListenHistoryConfig` | `MinimumFraction` | `double` | `0.5` | Min fraction for 'listened' status |
| `SoundCloudConfig` | `ClientId` | `string` | `null` | SoundCloud client ID |
| `SoundCloudConfig` | `AuthToken` | `string` | `null` | SoundCloud authentication token |
| `YouTubeMusicConfig` | `PlayerId` | `string` | `null` | Custom YouTube Music player ID |

### Theming and Color Schemes

SMoC supports custom themes and color schemes via Terminal.Gui's configuration format. You can customize colors globally or define theme-specific scheme overrides in `~/.config/smoc/config.json`.

<details>
<summary><strong>Custom Theme Configuration Example</strong> (Click to expand)</summary>

```json
{
  "Theme": "CustomDark",
  "Themes": {
    "CustomDark": {
      "Schemes": {
        "Accent": {
          "Normal": { "Foreground": "#ebdbb2", "Background": "None" },
          "Focus": { "Foreground": "#ebdbb2", "Background": "#639494" },
          "Active": { "Foreground": "#ebdbb2", "Background": "#394e4e" }
        },
        "ProgressBar": {
          "Normal": { "Foreground": "#ebdbb2", "Background": "#394e4e" }
        }
      }
    }
  }
}
```
</details>

#### Available Scheme Keys
You can override individual schemes either at the root level (`"Schemes": { ... }`) or scoped within a theme (`"Themes": { "<ThemeName>": { "Schemes": { ... } } }`). Any unspecified schemes automatically fall back to SMoC's default values:

| Scheme Key | Description |
| :--- | :---------- |
| `Accent` | Accent highlights across views |
| `ProgressBar` | Audio playback progress bars |
| `TableCurrentTrack` | Currently playing track in playlist and track tables |
| `TableHeaders` | Column headers in tables |
| `TableNormalTracks` | Normal track rows in tables |
| `Menu` | Navigation menus and dropdowns |
| `StatusBar` | Main status bar container |
| `StatusBar_State` | Playback state label within the status bar |
| `CommandLine` | Command line input prompt (`:`) |
| `CommandLineError` | Command line error messages and alerts |

## 🤝 Contributing

Contributions are welcome!
- **Code Style**: We follow the [Google C# Style Guide](https://google.github.io/styleguide/csharp-style.html).
- **Commits**: Please use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/).

## 📄 License

Distributed under the Apache-2.0 License. See `LICENSE` for more information.
