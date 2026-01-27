using Moq;
using smoc.Tests.TestInfra;
using Smoc.Services.Streaming;
using Smoc.Streaming;

namespace smoc.Tests.Services.Streaming;

public class StreamingListenHistoryServiceTest {

  private readonly Mock<IStreamingClient> _mockStreamingClient;

  public StreamingListenHistoryServiceTest() {
    _mockStreamingClient = new Mock<IStreamingClient>();
  }

  [Fact]
  public void PositionChanged_TooEarly_DoesNothing() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    sot.TrackPlayback(EntityTestFactory.GenerateSong(), TimeSpan.FromSeconds(10));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(It.IsAny<Song>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  [Fact]
  public void PositionChanged_TimeThresholdMet_AddsToListenHistory() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    var song = EntityTestFactory.GenerateSong();
    sot.TrackPlayback(song, TimeSpan.FromSeconds(31));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());
    _mockStreamingClient.VerifyNoOtherCalls();
  }

  [Fact]
  public void PositionChanged_FractionThresholdMet_AddsToListenHistory() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    var song = EntityTestFactory.GenerateSong(duration: TimeSpan.FromSeconds(30));
    sot.TrackPlayback(song, TimeSpan.FromSeconds(16));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());
    _mockStreamingClient.VerifyNoOtherCalls();
  }

  [Fact]
  public void PositionChanged_RepeatSong_CallsOnce() {
    var sot = new StreamingListenHistoryService(_mockStreamingClient.Object, TimeSpan.FromSeconds(30), 0.5f);
    var song = EntityTestFactory.GenerateSong(duration: TimeSpan.FromSeconds(30));
    sot.TrackPlayback(song, TimeSpan.FromSeconds(16));
    sot.TrackPlayback(song, TimeSpan.FromSeconds(17));
    _mockStreamingClient.Verify(client => client.AddToListenHistory(song, It.IsAny<CancellationToken>()), Times.Once());
    _mockStreamingClient.VerifyNoOtherCalls();
  }
}