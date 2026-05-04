# TODO: Subsonic API Integration

## Phase 1: Foundation & Configuration
- [x] Research current configuration management in smoc.
- [x] Add Subsonic configuration settings (Server URL, User, Password/Token).
- [x] Add 'ActiveService' setting to toggle between YTM and Subsonic.
- [x] Update Dependency Injection / Factory to instantiate the correct IStreamingClient.

## Phase 2: Subsonic Streaming Client
- [x] Define Subsonic API DTOs (Data Transfer Objects).
- [x] Implement Subsonic Authentication (MD5/Salt).
- [x] Implement IStreamingClient methods in SubsonicStreamingClient:
    - [x] Search (Artists, Albums, Songs)
    - [x] Get Artist details
    - [x] Get Album details
    - [x] Get Stream URL / Playback
    - [x] Scrobbling / Listen History

## Phase 3: Testing
- [x] Add Unit Tests for Subsonic Authentication and Mapping.
- [x] Add Service-Level Tests for SubsonicStreamingClient.
- [x] Add UI (Golden Master) Tests for Subsonic views.

## Phase 4: Finalization
- [x] Verify YTM functionality still works (Regression).
- [x] Submit Pull Request.
