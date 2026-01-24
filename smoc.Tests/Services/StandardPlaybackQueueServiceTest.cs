using Moq;
using smoc.Tests.Fakes;
using Smoc.Services;
using Smoc.Services.Audio;
using Smoc.Streaming;

namespace smoc.Tests.Services;

public class StandardPlaybackQueueServiceTest {

  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IAudioService> _mockAudioService;
  private readonly Mock<IStreamingClient> _mockStreamingClient;

  public StandardPlaybackQueueServiceTest() {
    _fakeMainWindow = new FakeMainWindow();
    _mockAudioService = new Mock<IAudioService>();
    _mockStreamingClient = new Mock<IStreamingClient>();
  }

  private StandardPlaybackQueueService NewStandardPlaybackQueue() => new(_fakeMainWindow, _mockStreamingClient.Object, _mockAudioService.Object);


}