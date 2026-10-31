using Moq;
using smoc.Tests.TestInfra;
using Smoc.Services.Streaming;
using Smoc.Streaming;

namespace smoc.Tests.Services.Streaming;

/// <summary>
/// Unit tests for <see cref="StreamingListenHistoryService"/>.
/// </summary>
public class StreamingListenHistoryServiceTest {

  private readonly Mock<IStreamingClient> _mockStreamingClient;

  /// <summary>
  /// Initializes a new instance of <see cref="StreamingListenHistoryServiceTest"/>.
  /// </summary>
  public StreamingListenHistoryServiceTest() {
    _mockStreamingClient = new Mock<IStreamingClient>();
  }

  /// <summary>
  /// Verifies that playback before the minimum position and fraction does not add to history.
  /// </summary>
  [Fact]
  public void PositionChanged_TooEarly_DoesNothing() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    sot.TrackPlayback(EntityTestFactory.GenerateSong(), TimeSpan.FromSeconds(10));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(It.IsAny<Song>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  /// <summary>
  /// Verifies that reaching the absolute time threshold adds the song to history.
  /// </summary>
  [Fact]
  public void PositionChanged_TimeThresholdMet_AddsToListenHistory() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    var song = EntityTestFactory.GenerateSong();
    sot.TrackPlayback(song, TimeSpan.FromSeconds(31));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());
    _mockStreamingClient.VerifyNoOtherCalls();
  }

  /// <summary>
  /// Verifies that reaching the fraction threshold adds the song to history.
  /// </summary>
  [Fact]
  public void PositionChanged_FractionThresholdMet_AddsToListenHistory() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    var song = EntityTestFactory.GenerateSong(duration: TimeSpan.FromSeconds(30));
    sot.TrackPlayback(song, TimeSpan.FromSeconds(16));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());
    _mockStreamingClient.VerifyNoOtherCalls();
  }

  /// <summary>
  /// Verifies that repeated updates for the same song do not duplicate history records.
  /// </summary>
  [Fact]
  public void PositionChanged_RepeatSong_CallsOnce() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    var song = EntityTestFactory.GenerateSong(duration: TimeSpan.FromSeconds(30));
    sot.TrackPlayback(song, TimeSpan.FromSeconds(16));
    sot.TrackPlayback(song, TimeSpan.FromSeconds(17));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());
    _mockStreamingClient.VerifyNoOtherCalls();
  }

  /// <summary>
  /// Verifies that a song with zero duration does not record when position is below minimum position.
  /// </summary>
  [Fact]
  public void PositionChanged_DurationZero_BelowMinimumPosition_DoesNotAddToListenHistory() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    var song = EntityTestFactory.GenerateSong(duration: TimeSpan.Zero);
    sot.TrackPlayback(song, TimeSpan.FromSeconds(10));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(It.IsAny<Song>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  /// <summary>
  /// Verifies that a song with zero duration records when minimum position is met.
  /// </summary>
  [Fact]
  public void PositionChanged_DurationZero_MinimumPositionMet_AddsToListenHistory() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    var song = EntityTestFactory.GenerateSong(duration: TimeSpan.Zero);
    sot.TrackPlayback(song, TimeSpan.FromSeconds(31));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());
    _mockStreamingClient.VerifyNoOtherCalls();
  }

  /// <summary>
  /// Verifies that resetting position to below threshold allows the song to be recorded again on subsequent play.
  /// </summary>
  [Fact]
  public void PositionChanged_ReplaySong_RecordsAgainAfterReset() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    var song = EntityTestFactory.GenerateSong(duration: TimeSpan.FromSeconds(60));

    // Initial playback crossing threshold
    sot.TrackPlayback(song, TimeSpan.FromSeconds(31));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());

    // Continuous playback does not record again
    sot.TrackPlayback(song, TimeSpan.FromSeconds(35));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());

    // Reset position to below threshold (replay)
    sot.TrackPlayback(song, TimeSpan.FromSeconds(5));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());

    // Playback crosses threshold again
    sot.TrackPlayback(song, TimeSpan.FromSeconds(31));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Exactly(2));
  }
}