
<div align="center">

# SMoC
### Steaming Music on Console

![License](https://img.shields.io/github/license/mrazza/smoc?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet)
![Status](https://img.shields.io/badge/status-active_development-green?style=flat-square)

A terminal-based music player (TUI) for streaming services, currently supporting **Subsonic**-compatible APIs and **YouTube Music**. Spotify, Apple Music, and other services are _future features_.

[Features](#-features) • [Installation](#-installation) • [Configuration and Setup](#%EF%B8%8F-configuration-and-setup) • [Usage](#-usage)

![screenshot](smoc_example.png)

</div>

---

> [!NOTE]
> This project is not supported or endorsed by any streaming music service or major company. It is a hobby project and has no affiliation with Spotify, Apple, Google, or YouTube.
> The same warnings and disclaimers for the well-known [yt-dlp](https://github.com/yt-dlp/yt-dlp) project apply here.

## 🌟 Features

- **TUI Interface**: Built with [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) for a rich console experience.
- **Cross-Platform Audio**: Powered by [SoundFlow](https://github.com/LSXPrime/SoundFlow).
- **YouTube Music Integration**: Powered by [YouTubeMusicAPI](https://github.com/IcySnex/YouTubeMusicAPI). Search, stream, and manage your library.
- **Subsonic API Integration**: Streaming client for any Subsonic-compatible server. For instance, [Navidrome](https://www.navidrome.org/), [Airsonic Advanced](https://github.com/airsonic-advanced/airsonic-advanced), [Gonic](https://github.com/sentriz/gonic), or [Madsonic](https://www.madsonic.org/).
- **Visuals**: Displays album art using Sixel graphics (requires a compatible terminal).

### Feature Progress
While most basic functionality is available and SMoC is totally usable, there's a lot left to do.

<details>
<summary><strong>Feature Plan</strong> (Click to expand)</summary>
  
- [ ] UI
  - [x] Search Support
  - [x] Command Bar
  - [x] Song Table
  - [x] Now Playing Bar
  - [x] Album Art (Sixel)
  - [x] Status Bar
  - [ ] Visualizations
  - [x] Now Playing Screen
- [ ] Playback
  - [x] Play
  - [x] Pause
  - [x] Stop
  - [x] Skip
  - [x] Previous
  - [x] Start Over
  - [ ] Repeat Track
  - [x] Volume
  - [ ] Playback Device Selection
  - [ ] Gapless Playback
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
Current release is [v0.1.0](https://github.com/mrazza/smoc/releases/tag/v0.1.0).

Available for [Windows x64](https://github.com/mrazza/smoc/releases/download/v0.1.0/smoc-v0.1.0-winx64-bin.zip), [Linux x64](https://github.com/mrazza/smoc/releases/download/v0.1.0/smoc-v0.1.0-linux64-bin.tar.gz), and [MacOS ARM64](https://github.com/mrazza/smoc/releases/download/v0.1.0/smoc-v0.1.0-macarm64-bin.zip).

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

SMoC operates with a Vim-style command bar. Press `:` to enter command mode.

### Navigation & Commands

| Command         | Description                                             |
| :-------------- | :------------------------------------------------------ |
| `:a`            | Switch to **Artist** mode                               |
| `:a/<artist>`   | Switch to **Artist** mode and search for `<artist>`     |
| `:t`            | Switch to **Track** mode                                |
| `:t/<track>`    | Switch to **Track** mode and search for `<track>`       |
| `:p`            | Switch to **Playlist** mode                             |
| `:p/<playlist>` | Switch to **Playlist** mode and search for `<playlist>` |
| `:likes`        | Load your **Liked Songs** playlist                      |
| `:url`          | Switch to **Playlist** mode from URL                    |
| `:url/<url>`    | Load songs from a specific YouTube Music URL            |
| `:pq`           | View the **Playback Queue**                             |
| `:np`           | View **Now Playing** screen                             |
| `:v/<0-100>`    | Set volume (e.g., `:v/80`)                              |
| `:q`            | **Quit** application                                    |

### Playback Controls

| Hotkey               | Action                                   |
| :------------------- | :--------------------------------------- |
| `Space`              | Play / Pause                             |
| `Ctrl+Space`         | Stop                                     |
| `,` (Comma)          | Previous Track (Restart if > 10s played) |
| `.` (Period)         | Next Track                               |
| `[`                  | Seek Backward 10s                        |
| `]`                  | Seek Forward 10s                         |
| `Up/Down/Left/Right` | Navigate Tables                          |
| `h/j/k/l`            | Navigate Tables (Vim style)              |
| `Shift + Nav Key`    | Select multiple items                    |
| `Tab`                | Switch active pane                       |
| `Enter`              | Open Song Context Menu for selected item |

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

### Subsonic
To use a Subsonic-compatible service (like Navidrome, Gonic, or Airsonic):
1. Ensure your server has the Subsonic API enabled.
2. Configure your server hostname, port, scheme, username, and password in `config.json` (see below).
3. Set `SmocConfiguration.ActiveService` to `Subsonic`.

### YouTube Music
To use YouTube Music:
1. Set `SmocConfiguration.ActiveService` to `YouTubeMusic`.
2. Setup required settings for YouTube Music authentication (see below).

#### Authentication

> [!NOTE]
> You need a YouTube Music Premium subscription to play most content.

To access your YouTube Music account, you need to extract your cookies from YouTube Music (these steps are copied from [YouTube.js](https://ytjs.dev/guide/authentication.html)):

1. Open [YouTube Music](https://music.youtube.com) in your browser (Incognito recommended).
2. Open Developer Tools (`F12`) -> **Network** tab.
3. Find a `POST` request to `music.youtube.com`.
4. Copy the value of the `cookie` request header.
5. Save it to `~/.config/smoc/cookie.txt`.

> [!TIP]
> It is recommended to open an incognito window or separate browser profile when getting your cookie information to avoid other browser sessions from invalidating the tokens. Once cookies are retrieved, close the session _without_ logging out.

More recently, additional tokens beyond just cookies are often needed. POToken and Visitor Data are required in the `tokens.json` file in the same directory as your `cookie.txt`.

> [!TIP]
> Run `smoc --gentokens` after setting up the cookie to generate necessary PO Tokens and Visitor Data automatically.

### Config File
Create or edit `~/.config/smoc/config.json` to customize settings.

<details>
<summary><strong>Example Configuration</strong> (Click to expand)</summary>

```json
{
    "SmocConfiguration.LogLevel": "Warning",
    "SmocConfiguration.SongCacheSizeBytes": 1073741824,
    "SmocConfiguration.AlbumCoverCacheSizeBytes": 1073741824,
    "SmocConfiguration.SongCacheMaxElements": 1000,
    "SmocConfiguration.AlbumCoverCacheMaxElements": 1000,
    "SmocConfiguration.ActiveService": "Subsonic",
	"SubsonicConfig.ServerHost": "localhost",
	"SubsonicConfig.ServerPort": 8080,
	"SubsonicConfig.Username": "username",
	"SubsonicConfig.Password": "password",
    "Theme": "gruvbox-custom",
    "Themes": [
        {
            "gruvbox-custom": {
                "Schemes": [
                    {
                        "Accent": {
                            "Normal": { "Foreground": "#ebdbb2", "Background": "#00000000" },
                            "Focus": { "Foreground": "#ebdbb2", "Background": "#639494" },
                            "Active": { "Foreground": "#ebdbb2", "Background": "#394e4e" }
                        }
                    }
                    // ... (other schemes)
                ]
            }
        }
    ]
}
```
</details>

#### Common Settings
| Category           | Key                                            | Type               | Description                                                             |
| :----------------- | :--------------------------------------------- | :----------------- | :---------------------------------------------------------------------- |
| **Streaming**      | `SmocConfiguration.ActiveService`              | `StreamingService` | Active service (`YouTubeMusic`, `Subsonic`)                             |
| **Subsonic**       | `SubsonicConfig.ServerScheme`                  | `string`           | URI Scheme for the Subsonic API (default: http)                         |
|                    | `SubsonicConfig.ServerHost`                    | `string`           | Hostname of your Subsonic server                                        |
|                    | `SubsonicConfig.ServerPort`                    | `int`              | Port of your Subsonic server (default: 80)                              |
|                    | `SubsonicConfig.Username`                      | `string`           | Subsonic username                                                       |
|                    | `SubsonicConfig.Password`                      | `string`           | Subsonic password                                                       |
|                    | `SubsonicConfig.UseToken`                      | `bool`             | Whether to use token auth instead of plaintext password (default: true) |
| **Caching**        | `SmocConfiguration.SongCacheSizeBytes`         | `long`             | Max size of song cache in bytes (0 = no limit)                          |
|                    | `SmocConfiguration.AlbumCoverCacheSizeBytes`   | `long`             | Max size of album cover cache in bytes (0 = no limit)                   |
|                    | `SmocConfiguration.SongCacheMaxElements`       | `int`              | Max number of songs to cache (0 = no limit)                             |
|                    | `SmocConfiguration.AlbumCoverCacheMaxElements` | `int`              | Max number of album covers to cache (0 = no limit)                      |
| **Logging**        | `SmocConfiguration.LogLevel`                   | `LogLevel`         | Min log level (Trace, Debug, Information, Warning, Error, Critical)     |
| **Listen History** | `ListenHistory.Enabled`                        | `bool`             | Whether listen history tracking is enabled                              |
|                    | `ListenHistory.MinimumPositionSeconds`         | `int`              | Minimum position (seconds) to consider listened                         |
|                    | `ListenHistory.MinimumFraction`                | `double`           | Minimum fraction of a song to consider listened                         |
| **UI**             | `Theme`                                        | `string`           | The name of the theme to use (default: `default`)                       |

#### Custom Themes
If specifying a custom theme (as in the example config above), you will need to specify styling for all schemes.

<details>
<summary><strong>Example Theme</strong> (Click to expand)</summary>

```json
{
    "Theme": "gruvbox-custom",
    "Themes": [
        {
            "gruvbox-custom": {
                "Schemes": [
                    {
                        "Accent": {
                            "Normal": {
                                "Foreground": "#ebdbb2",
                                "Background": "#00000000"
                            },
                            "Focus": {
                                "Foreground": "#ebdbb2",
                                "Background": "#639494"
                            },
                            "Active": {
                                "Foreground": "#ebdbb2",
                                "Background": "#394e4e"
                            }
                        }
                    },
                    {
                        "Base": {
                            "Normal": {
                                "Foreground": "#ebdbb2",
                                "Background": "#00000000"
                            }
                        }
                    },
                    {
                        "TableCurrentTrack": {
                            "Normal": {
                                "Foreground": "#ebdbb2",
                                "Background": "#394e4e",
                                "Style": "Bold"
                            },
                            "Focus": {
                                "Foreground": "#ebdbb2",
                                "Background": "#639494",
                                "Style": "Bold"
                            },
                            "Active": {
                                "Foreground": "#ebdbb2",
                                "Background": "#394e4e",
                                "Style": "Bold"
                            }
                        }
                    },
                    {
                        "TableNormalTracks": {
                            "Normal": {
                                "Foreground": "#ebdbb2",
                                "Background": "#00000000"
                            },
                            "Focus": {
                                "Foreground": "#ebdbb2",
                                "Background": "#639494"
                            },
                            "Active": {
                                "Foreground": "#ebdbb2",
                                "Background": "#394e4e"
                            }
                        }
                    },
                    {
                        "Menu": {
                            "Normal": {
                                "Foreground": "#ebdbb2",
                                "Background": "#3a3a3a"
                            },
                            "Focus": {
                                "Foreground": "#ebdbb2",
                                "Background": "#639494"
                            },
                            "Active": {
                                "Foreground": "#ebdbb2",
                                "Background": "#394e4e"
                            }
                        }
                    },
                    {
                        "StatusBar": {
                            "Normal": {
                                "Foreground": "#949494",
                                "Background": "#3a3a3a"
                            }
                        }
                    },
                    {
                        "StatusBar_Mode": {
                            "Normal": {
                                "Foreground": "#262626",
                                "Background": "#949494",
                                "Style": "Bold"
                            }
                        }
                    },
                    {
                        "CommandLine": {
                            "Normal": {
                                "Foreground": "#ebdbb2",
                                "Background": "#00000000"
                            },
                            "Editable": {
                                "Foreground": "#ebdbb2",
                                "Background": "#00000000"
                            }
                        }
                    },
                    {
                        "CommandLineError": {
                            "Normal": {
                                "Foreground": "#262626",
                                "Background": "#d75f5f",
                                "Style": "Bold"
                            }
                        }
                    }
                ]
            }
        }
    ]
}
```
</details>

## 🤝 Contributing

Contributions are welcome!
- **Code Style**: We follow the [Google C# Style Guide](https://google.github.io/styleguide/csharp-style.html).
- **Commits**: Please use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/).

## 📄 License

Distributed under the Apache-2.0 License. See `LICENSE` for more information.
