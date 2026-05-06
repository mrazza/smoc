# Plan: SoundCloud Integration for SMoC

## 1. Product Requirements

### Background & Relevance
SoundCloud is a unique streaming platform known for its massive library of indie, underground, and remixed music that is often unavailable on mainstream services like YouTube Music or Spotify. Adding SoundCloud support expands SMoC's reach to users who follow specific music scenes (e.g., electronic, hip-hop, lo-fi).

### Feature Scope
*   **Search**: Search for tracks, artists, and playlists.
*   **Playback**: Stream public tracks using the SoundCloud stream API.
*   **User Integration**: (Optional Phase 2) Access user-specific "Likes" and private playlists.
*   **Metadata**: Fetch artist info, track duration, and high-quality artwork.

---

## 2. Technical Implementation Plan

### Architecture Overview
SoundCloud integration will be implemented via `SoundCloudStreamingClient.cs`. Unlike YouTube Music, SoundCloud's public API is relatively accessible for playback via its `stream_url` endpoint, provided a valid `client_id` is available.

### Proposed Code Changes

#### 1. Configuration (`Smoc.Configuration`)
*   Create `SoundCloudConfig.cs`:
    *   `ClientId`: Required for API requests.
    *   `AuthToken`: (Optional) For accessing user-specific content.

#### 2. Streaming Client (`Smoc.Streaming.SoundCloud`)
*   **`SoundCloudStreamingClient.cs`**:
    *   Implement `IStreamingClient`.
    *   **Search**: Use the `v2/search` or `v2/tracks` endpoints.
    *   **Playback**: Fetch the stream URL. Note that SoundCloud often returns a redirect to a transcoded HLS or progressive MP3 stream. SMoC's `SoundFlow` (via FFMpeg) can handle these.
    *   **Entity Mapping**: Map SoundCloud's `track` object to SMoC's `Song`, `Album` (using a "SoundCloud Uploads" placeholder album if necessary), and `Artist`.

### Libraries & Dependencies
*   **SoundCloud.Api** (NuGet): A .NET wrapper for the SoundCloud API.
*   **SoundFlow.Codecs.FFMpeg**: Used to decode the resulting audio streams.

### Implementation Milestones
1.  **Client Discovery**: Implement logic to rotate/refresh a `client_id` (as SoundCloud rotates these on their web player).
2.  **Basic Playback**: Implement `GetSongStreamAsync` for a specific track ID.
3.  **Search Integration**: Connect `SearchSongsAsync` and `SearchArtistsAsync`.
4.  **Artwork**: Map SoundCloud's `artwork_url` to SMoC's `GetAlbumArtAsync`.

---

## 3. Future Considerations
*   **HLS Streaming**: Ensuring `SoundFlow` properly handles HLS-segmented streams which are common on newer SoundCloud uploads.
### API Call Details

1. **Client ID Discovery**
   - **Logic**: If the provided `ClientId` fails, fetch `https://soundcloud.com` and parse the HTML/JS to find the latest `client_id` embedded in the scripts.

2. **Search**
   - **Endpoint**: `GET https://api-v2.soundcloud.com/search`
   - **Parameters**: `q`, `client_id`, `limit`, `offset`

3. **Track Details**
   - **Endpoint**: `GET https://api-v2.soundcloud.com/tracks/{trackId}`
   - **Parameters**: `client_id`

4. **Streaming**
   - **Endpoint**: `GET https://api-v2.soundcloud.com/tracks/{trackId}/streams`
   - **Parameters**: `client_id`
   - **Returns**: A list of protocol links (e.g., `hls`, `progressive`).
   - **Usage**: Follow the `progressive` link (if available) to get the final MP3 URL, or the `hls` link to get an M3U8 manifest.

### Test Plan

1. **Unit Tests**
   - **Client ID Extraction**: Test the regex/parser against various SoundCloud home page HTML snapshots.
   - **Metadata Mapping**: Verify mapping of SoundCloud tracks and "sets" (playlists) to SMoC entities.

2. **Integration Tests**
   - **HLS Playback**: Verify that `SoundFlow` (via FFMpeg) can consume the M3U8 streams provided by SoundCloud.
