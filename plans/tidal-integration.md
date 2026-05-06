# Plan: Tidal Integration for SMoC

## 1. Product Requirements

### Background & Relevance
Tidal is the premier service for high-fidelity audio. SMoC users who value terminal minimalist UI often overlap with audiophiles who appreciate Tidal's FLAC and Master quality streams. Supporting Tidal positions SMoC as a "Hi-Fi CLI Player."

### Feature Scope
*   **High-Quality Playback**: Support for AAC and Lossless FLAC streams.
*   **Device Authentication**: Implementation of the "Device Code" OAuth flow (best for terminal apps without a browser).
*   **Library Management**: Access to user's "My Collection" (Artists, Albums, Playlists).

---

## 2. Technical Implementation Plan

### Architecture Overview
Tidal uses a complex OAuth-based authentication system. The `TidalStreamingClient` will need to handle persistent tokens and refresh cycles.

### Proposed Code Changes

#### 1. Configuration (`Smoc.Configuration`)
*   Create `TidalConfig.cs`:
    *   `ClientId` / `ClientSecret`: Required for OAuth.
    *   `AccessToken` / `RefreshToken`: Stored securely to avoid re-auth.
    *   `QualitySetting`: (Low, High, Lossless).

#### 2. Streaming Client (`Smoc.Streaming.Tidal`)
*   **`TidalStreamingClient.cs`**:
    *   Implement `IStreamingClient`.
    *   **Authentication**: Implement the `device/authorization` flow.
    *   **Metadata**: Use Tidal's `/tracks`, `/albums`, and `/artists` endpoints.
    *   **Playback**: Call the `/playbackinfo` endpoint to receive the stream manifest. Tidal provides streams in encrypted/protected formats, but standard AAC/FLAC URLs are available for standard API clients.

### Libraries & Dependencies
*   **System.Net.Http**: For REST API calls.
*   **Newtonsoft.Json** or **System.Text.Json**: For complex manifest parsing.

### Implementation Milestones
1.  **OAuth Handshake**: Implement the terminal-based "Go to this URL and enter code" flow.
2.  **Catalog Browsing**: Map Tidal's rich metadata to SMoC entities.
3.  **Lossless Streaming**: Ensure `SoundFlow` is configured to handle FLAC streams from Tidal.
4.  **Listen History**: Implement scrobbling to Tidal's history service.

---

## 3. Future Considerations
*   **MQA Support**: (Low priority) Handling Master Quality Authenticated streams if required.
### API Call Details

1. **Device Authorization**
   - **Endpoint**: `POST https://auth.tidal.com/v1/oauth2/device/authorization`
   - **Parameters**: `client_id`, `scope=user`
   - **Returns**: `deviceCode`, `userCode`, `verificationUri`, `expiresIn`, `interval`
   - **Usage**: Display `userCode` and `verificationUri` to the user. Poll for token using `deviceCode`.

2. **Token Exchange/Refresh**
   - **Endpoint**: `POST https://auth.tidal.com/v1/oauth2/token`
   - **Parameters**: 
     - Auth: `grant_type=urn:ietf:params:oauth:grant-type:device_code`, `device_code`, `client_id`
     - Refresh: `grant_type=refresh_token`, `refresh_token`, `client_id`
   - **Returns**: `access_token`, `refresh_token`, `expires_in`

3. **Search**
   - **Endpoint**: `GET https://api.tidal.com/v1/search`
   - **Parameters**: `query`, `types=TRACKS,ARTISTS,ALBUMS`, `limit`, `countryCode`
   - **Headers**: `Authorization: Bearer <token>`

4. **Playback Info**
   - **Endpoint**: `GET https://api.tidal.com/v1/tracks/{trackId}/playbackinfo`
   - **Parameters**: `audioquality=LOSSLESS`, `playbackmode=STREAM`, `assetpresentation=FULL`
   - **Returns**: A JSON object containing a `manifest` (Base64 encoded). 
   - **Processing**: Decode manifest to find the stream URL (usually an S3 signed URL or MPEG-DASH).

### Test Plan

1. **Unit Tests (Smoc.Tests.Streaming.Tidal)**
   - **MockTidalHttpHandler**: A custom `HttpMessageHandler` to return canned JSON responses for all endpoints.
   - **Auth Flow State Machine**: Verify that the client correctly handles the polling interval and transitions from "Pending" to "Authenticated".
   - **Token Expiry**: Verify that the client automatically uses the `refresh_token` when a 401 is received or when the token is near expiry.
   - **Mapping Tests**: Ensure all Tidal API fields (e.g., `artist.name`, `album.title`) correctly map to `Smoc.Streaming` entities.

2. **Integration Tests**
   - **Stream Extraction**: Test the manifest parser with real (captured) Base64 payloads to ensure URL extraction is robust across different stream types (AAC vs FLAC).
   - **End-to-End (Manual)**: Verify playback in the SMoC UI using a Tidal developer account and the device flow.
