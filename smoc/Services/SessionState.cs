using Smoc.Streaming;

namespace Smoc.Services;

/// <summary>
/// Represents the persisted playback session state across application restarts.
/// </summary>
/// <param name="Queue">The list of songs in the playback queue.</param>
/// <param name="CurrentPlaybackIndex">The 0-based index of the active song in the queue.</param>
/// <param name="CurrentPositionSeconds">The playback position of the active song in seconds.</param>
/// <param name="Volume">The master volume level (0.0 to 2.0).</param>
public sealed record SessionState(
  List<Song> Queue,
  int CurrentPlaybackIndex,
  double CurrentPositionSeconds,
  float Volume);
