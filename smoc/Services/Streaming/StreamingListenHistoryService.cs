using Smoc.Streaming;
using Terminal.Gui.App;

namespace Smoc.Services.Streaming;

/// <summary>
/// Tracks song playback in the user's streaming service by adding it to the user's listening history.
/// </summary>
public class StreamingListenHistoryService : IPlaybackTrackingService {
  private readonly IStreamingClient _client;
  private Song? _lastRecordedTrack;
  private readonly TimeSpan _minimumPosition;
  private readonly double _minimumFraction;

  /// <summary>
  /// Creates a new instance of the <see cref="StreamingListenHistoryService"/> class.
  /// </summary>
  /// <param name="client">The streaming client to use for tracking playback.</param>
  /// <param name="minimumPosition">The minimum position for a song to be considered listened to.</param>
  /// <param name="minimumFraction">The minimum fraction of a song for it to be considered listened to.</param>
  public StreamingListenHistoryService(IStreamingClient client, TimeSpan minimumPosition, double minimumFraction) {
    _client = client;
    _lastRecordedTrack = null;
    _minimumPosition = minimumPosition;
    _minimumFraction = minimumFraction;
  }

  /// <inheritdoc/>
  public async void TrackPlayback(Song song, TimeSpan position) {
    if (song == _lastRecordedTrack) return;

    if (position < _minimumPosition && position / song.Duration < _minimumFraction) return;

    _lastRecordedTrack = song;
    try {
      Logging.Information($"Adding song to listen history: {song.Title}");
      await _client.AddToListenHistory(song);
    } catch (Exception ex) {
      Logging.Error($"Failed to add song to listen history: {ex.Message}");
    }
  }
}