# SMoC
Steaming Music on Console

A TUI music player for streaming services. The currently implemented streaming service is YouTube Music (YTM).

![screenshot](smoc_example.png)

This project is not supported or endorsed by Google. The same warnings and disclamers for the well-known [yt-dlp](https://github.com/yt-dlp/yt-dlp) project apply here.

## Commands
SMoC operates via a vim-style command bar. Switching between views and performing many actions occur via the bottom command bar. Much like vim, hitting the `:` key will activate COMMAND mode. The following commands are supported:
- `:a`: Switch to the ARTIST mode
- `:a/<artist name>`: Switch to the ARTIST mode and search for a given artist
- `:t`: Switch to the TRACK mode
- `:t/<track name>`: Switch to the TRACK mode and search for a given track
- `:np`: Switch to the now playing mode (current playback queue)
- `:v/<volume>`: Change volume to the specified value

## Cookies
In order to play most tracks, you will need to authenticate with a Google acocunt that has a value YouTube Music Subscription.

### Browser Auth Setup Steps
1. Open YouTube Music in your browser - ensure you are logged in.
1. Open web developer tools (F12).
1. Open Network tab and locate a POST request to `music.youtube.com`.
1. Copy the `Cookie` into a text file named `cookie.txt` into your local smoc config directory. Note you will need to create the directory if it does not exist. This will usually be `~/.config/smoc/cookie.txt`.

### PO Token and Visitor Data
More recently, additional tokens are often needed. SMoC can generate these for you if you have provided a valid cookie in the `cookie.txt` file. Run `smoc --gentokens`; this will create a `tokens.json` file in the same config directory.
