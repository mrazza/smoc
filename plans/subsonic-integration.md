# Plan: Subsonic API Integration for SMoC

## 1. Product Requirements

### Background & Relevance
SMoC (Simple Music on Console) currently supports YouTube Music. However, a significant portion of terminal-based music player users prefer self-hosted solutions to maintain privacy and control over their high-quality music libraries. 

The **Subsonic API** is the de-facto standard for self-hosted music streaming. By implementing support for Subsonic, SMoC immediately gains compatibility with a wide range of popular music servers, including:
*   **Navidrome** (Modern, lightweight)
*   **Jellyfin** (via its Subsonic endpoint)
*   **Airsonic / Airsonic-Advanced**
*   **Gonic**
*   **Lidarr**

### Feature Scope
*   **Server Connectivity**: Connect to any Subsonic-compatible server via URL and credentials.
*   **Library Browsing**: Search for Artists, Albums, and Songs.
*   **Playback**: Stream audio in various formats (MP3, FLAC, Opus) as provided by the server.
*   **Authentication**: Support for both cleartext passwords (less secure) and Token-based (MD5 salt) authentication (standard for Subsonic).
*   **Art**: Display album artwork fetched from the server.
*   **Social**: Support for Liking/Starring tracks and scrobbling (updating "Now Playing" on the server).

---

## 2. Technical Implementation Plan

### Architecture Overview
A new `SubsonicStreamingClient` will be added, implementing the existing `IStreamingClient` interface. This client will use `HttpClient` to communicate with the Subsonic REST API (v1.16.1+ recommended).

### Proposed Code Changes

#### 1. Configuration (`Smoc.Configuration`)
*   Create `SubsonicConfig.cs`:
    *   `ServerUrl`: The base URL of the Subsonic server.
    *   `Username`: User account name.
    *   `Password`: User password (to be hashed/salted).
    *   `ApiVersion`: Target Subsonic API version (default 1.16.1).

#### 2. Streaming Client (`Smoc.Streaming.Subsonic`)
*   `SubsonicStreamingClient.cs`:
    *   Implement `IStreamingClient`.
    *   Use `HttpClient` for all requests.
    *   Handle authentication via `u`, `t`, and `s` query parameters (Token/Salt method).
    *   **Search**: Map `search3.view` results to SMoC's `Artist`, `Album`, and `Song` records.
    *   **Playback**: Use the `stream.view` endpoint to return a `SongStream`.
    *   **Images**: Use `getCoverArt.view` for album/artist artwork.

#### 3. Data Transfer Objects (DTOs)
*   Define internal records to deserialize Subsonic JSON responses (using `System.Text.Json`).

### Libraries & Dependencies
*   **Native .NET**: No external Subsonic-specific libraries are strictly required as the REST API is straightforward. Using `HttpClient` and `System.Text.Json` keeps the project lightweight.
*   **SixLabors.ImageSharp**: Already used in SMoC; will be used to decode images fetched from the Subsonic server.

### Implementation Milestones
1.  **Auth Layer**: Implement the salt/token generation logic.
2.  **Basic Search**: Implement `SearchArtistsAsync`, `SearchSongsAsync`, and `SearchPlaylistsAsync`.
3.  **Playback**: Implement `GetSongStreamAsync` and verify `SoundFlow` can handle the returned streams.
4.  **Metadata/Art**: Implement album art fetching and detailed metadata lookups.
5.  **History**: Implement `AddToListenHistory` using the Subsonic `scrobble.view` or `setRating.view` endpoints.

---

## 3. Future Considerations
*   **Offline Cache**: Potential for local caching of Subsonic streams for offline use.
*   **Transcoding**: Supporting server-side transcoding requests if the user is on a low-bandwidth connection.
