namespace Smoc.Services;

/// <summary>
/// Represents the current state of audio playback.
/// </summary>
public enum PlaybackState {
  /// <summary>
  /// Playback is stopped. No audio is playing and no position is tracked.
  /// </summary>
  Stopped,

  /// <summary>
  /// Audio is currently playing.
  /// </summary>
  Playing,

  /// <summary>
  /// Playback is paused. Position is maintained.
  /// </summary>
  Paused
}
