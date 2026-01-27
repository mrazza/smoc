using Smoc.Streaming;

namespace Smoc.Services;

/// <summary>
/// Interface for services that track song playback.
/// </summary>
/// <remarks>
/// Services implementing this interface track playback events
/// and can be used to update the user's listening history.
/// 
/// e.g. scrobble to Last.fm or update a streaming service's
/// listening history.
/// </remarks>
public interface IPlaybackTrackingService {
  /// <summary>
  /// Tracks the playback of a song.
  /// </summary>
  /// <remarks>
  /// This is called regularly during playback allowing implementation-specific
  /// behavior for tracking playback.
  /// </remarks>
  /// <param name="song">The song to track.</param>
  /// <param name="position">The position in the song.</param>
  void TrackPlayback(Song song, TimeSpan position);
}
