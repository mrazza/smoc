using Moq;
using Sharpcaster.Models.Media;
using Smoc.Services.Audio.Cast;
using Smoc.Services.Cast;
using Smoc.Streaming;
using smoc.Tests.TestInfra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace smoc.Tests.Services.Audio.Cast;

public class CastPlaybackServiceTest {
  private readonly Mock<IStreamingProxyService> _mockProxyService;
  private readonly Mock<IChromecastClient> _mockClient;
  private readonly Song _song;
  private readonly MemoryStream _stream;
  private readonly string _url = "http://proxy/stream";

  public CastPlaybackServiceTest() {
    _mockProxyService = new Mock<IStreamingProxyService>();
    _mockClient = new Mock<IChromecastClient>();
    _song = EntityTestFactory.GenerateSong();
    _stream = new MemoryStream();
  }

  [Fact]
  public void InitialState_IsStopped() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    Assert.Equal(Smoc.Services.PlaybackState.Stopped, sut.PlaybackState);
    Assert.Equal(_song, sut.Song);
    Assert.Equal(TimeSpan.Zero, sut.CurrentTime);
  }

  [Fact]
  public async Task Play_UpdatesStateToPlaying() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    sut.Play();
    await sut.WaitForPendingCommandsAsync();

    Assert.Equal(Smoc.Services.PlaybackState.Playing, sut.PlaybackState);
    _mockClient.Verify(c => c.LoadAsync(It.IsAny<Media>()), Times.Once);
  }

  [Fact]
  public async Task Pause_UpdatesStateToPaused() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    sut.Play();
    sut.Pause();
    await sut.WaitForPendingCommandsAsync();

    Assert.Equal(Smoc.Services.PlaybackState.Paused, sut.PlaybackState);
    _mockClient.Verify(c => c.PauseAsync(), Times.Once);
  }

  [Fact]
  public async Task Stop_UpdatesStateToStopped() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    sut.Play();
    sut.Stop();
    await sut.WaitForPendingCommandsAsync();

    Assert.Equal(Smoc.Services.PlaybackState.Stopped, sut.PlaybackState);
    _mockClient.Verify(c => c.StopAsync(), Times.Once);
  }

  [Fact]
  public async Task StopThenPlay_ExecutesCommandsInStrictSequentialOrder() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    var executionLog = new List<string>();
    var stopTcs = new TaskCompletionSource();

    var stopStartedTcs = new TaskCompletionSource();

    _mockClient.Setup(c => c.StopAsync()).Returns(async () => {
      executionLog.Add("StopStarted");
      stopStartedTcs.SetResult();
      await stopTcs.Task;
      executionLog.Add("StopFinished");
    });

    _mockClient.Setup(c => c.LoadAsync(It.IsAny<Media>())).Returns(() => {
      executionLog.Add("PlayStarted");
      return Task.CompletedTask;
    });

    sut.Stop();
    sut.Play();

    await stopStartedTcs.Task;

    // Verify Stop was initiated but Play has not started because Stop is still executing
    Assert.Equal(new[] { "StopStarted" }, executionLog);

    // Release StopAsync to complete
    stopTcs.SetResult();
    await sut.WaitForPendingCommandsAsync();

    // Verify strict serial execution order
    Assert.Equal(new[] { "StopStarted", "StopFinished", "PlayStarted" }, executionLog);
  }

  [Fact]
  public async Task FaultedCommand_DoesNotBreakSubsequentCommands() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    _mockClient.Setup(c => c.StopAsync()).ThrowsAsync(new InvalidOperationException("Network failure"));
    _mockClient.Setup(c => c.LoadAsync(It.IsAny<Media>())).Returns(Task.CompletedTask);

    sut.Stop();
    sut.Play();

    await sut.WaitForPendingCommandsAsync();

    _mockClient.Verify(c => c.StopAsync(), Times.Once);
    _mockClient.Verify(c => c.LoadAsync(It.IsAny<Media>()), Times.Once);
    Assert.Equal(Smoc.Services.PlaybackState.Playing, sut.PlaybackState);
  }

  [Fact]
  public void Dispose_StopsProxyAndDisposesStream() {
    var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    sut.Dispose();

    _mockProxyService.Verify(p => p.StopProxy(_url), Times.Once);
    Assert.Throws<ObjectDisposedException>(() => _stream.Read(new byte[1], 0, 1));
  }
}
