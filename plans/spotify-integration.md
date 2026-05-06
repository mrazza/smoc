# Plan: Spotify Integration for SMoC

## 1. Product Requirements

### Background & Relevance
Spotify is the world's most popular streaming service. While it is the most difficult to integrate due to its lack of direct audio stream access via the Web API, it is the most requested feature for any music player. 

### Feature Scope
*   **Full Catalog Access**: Search and play any song on Spotify.
*   **Librespot Integration**: Use the `librespot` open-source protocol to function as a "Spotify Connect" device.
*   **Authentication**: Support for Spotify Premium accounts (required by librespot).

---

## 2. Technical Implementation Plan

### Architecture Overview
Unlike other providers, Spotify cannot be implemented via simple REST calls for playback. The `SpotifyStreamingClient` will act as a bridge to a `Librespot-DotNet` instance.

### Proposed Code Changes

#### 1. Configuration (`Smoc.Configuration`)
*   Create `SpotifyConfig.cs`:
    *   `Username` / `Password`: Required for Librespot authentication.
    *   `CacheDirectory`: For Spotify's encrypted blob cache.

#### 2. Streaming Client (`Smoc.Streaming.Spotify`)
*   **`SpotifyStreamingClient.cs`**:
    *   Implement `IStreamingClient`.
    *   **Metadata**: Use the official **Spotify Web API** for searching and entity lookups (faster and better documented).
    *   **Playback**: `GetSongStreamAsync` will trigger `Librespot` to fetch the track and provide the PCM/Ogg stream.
    *   **Challenge**: Librespot usually outputs directly to a sound device. SMoC will need to configure it to output to a `PipeStream` or `MemoryStream` that `SoundFlow` can consume.

### Libraries & Dependencies
*   **SpotifyAPI-NET**: For Web API metadata and search.
*   **Librespot-DotNet**: For the actual playback engine.

### Implementation Milestones
1.  **Web API Integration**: Get search and metadata working first using Client Credentials.
2.  **Librespot Service**: Integrate the Librespot player into SMoC's service layer.
3.  **Stream Redirection**: Redirect Librespot's audio output into SMoC's `SoundFlow` engine.
4.  **Connect Support**: Allow SMoC to appear as a "Spotify Connect" device in the official Spotify app.

---

## 3. Future Considerations
*   **Premium Requirement**: Clear UI feedback that a Spotify Premium account is required for playback.
### Librespot Stream Extraction

To get a `Stream` from `Librespot-DotNet`, we will:
1. Implement a custom `IAudioOutput` (or equivalent interface in the library) that wraps a `PipeStream`.
2. As Librespot decodes the Ogg/Vorbis or AAC stream from Spotify's servers into raw PCM, our `IAudioOutput` will write those bytes into the `PipeStream.Writer`.
3. The `SpotifyStreamingClient.GetSongStreamAsync` method will return the `PipeStream.Reader` as part of the `SongStream` object.
4. SMoC's `SoundFlow` engine will read from this stream as if it were a local file or network stream.

### Test Plan

1. **Unit Tests (Smoc.Tests.Streaming.Spotify)**
   - **Web API Mocks**: Mock the Spotify Web API responses for search and metadata using `SpotifyAPI-NET`'s testing utilities.
   - **Buffer Management**: Test the `PipeStream` implementation to ensure it handles backpressure correctly if `SoundFlow` reads slower than Librespot decodes.
   - **Auth Handling**: Verify that credentials are correctly passed to the Librespot session manager.

2. **Integration Tests**
   - **Librespot Lifecycle**: Test starting and stopping the Librespot player instance to ensure no memory leaks or orphaned network connections.
   - **Stream Continuity**: Verify that a full song can be read from the `PipeStream` without corruption.
