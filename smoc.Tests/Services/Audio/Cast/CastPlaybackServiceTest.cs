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
  public async Task CurrentTime_AdvancesWhilePlaying() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    sut.Play();
    await sut.WaitForPendingCommandsAsync();

    // Wait a short duration for the progress tracking timer to advance
    await Task.Delay(100, TestContext.Current.CancellationToken);

    Assert.True(sut.CurrentTime > TimeSpan.Zero, "CurrentTime should advance while in Playing state.");
    Assert.True(sut.Progress > 0, "Progress should be greater than zero.");
  }

  [Fact]
  public async Task CurrentTime_FreezesOnPause() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    sut.Play();
    await sut.WaitForPendingCommandsAsync();
    await Task.Delay(100, TestContext.Current.CancellationToken);

    sut.Pause();
    await sut.WaitForPendingCommandsAsync();

    var pausedTime = sut.CurrentTime;
    await Task.Delay(100, TestContext.Current.CancellationToken);

    Assert.Equal(pausedTime, sut.CurrentTime);
  }

  [Fact]
  public async Task CurrentTime_CalibratesOnMediaStatusChanged() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    sut.Play();
    await sut.WaitForPendingCommandsAsync();

    // Trigger MediaStatusChanged event from client with specific currentTime
    _mockClient.Raise(c => c.MediaStatusChanged += null, _mockClient.Object, new MediaStatus {
      CurrentTime = 42.5,
      PlayerState = PlayerStateType.Playing,
      Media = new Media { Duration = 180.0 }
    });

    Assert.True(sut.CurrentTime >= TimeSpan.FromSeconds(42.5));
    Assert.Equal(TimeSpan.FromSeconds(180.0), sut.Duration);
  }

  [Fact]
  public async Task Seek_UpdatesCurrentTime() {
    using var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    sut.Play();
    await sut.WaitForPendingCommandsAsync();

    sut.Seek(TimeSpan.FromSeconds(60));
    await sut.WaitForPendingCommandsAsync();

    Assert.True(sut.CurrentTime >= TimeSpan.FromSeconds(60));
  }

  [Fact]
  public void Dispose_StopsProxyAndDisposesStream() {
    var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);

    sut.Dispose();

    _mockProxyService.Verify(p => p.StopProxy(_url), Times.Once);
    Assert.Throws<ObjectDisposedException>(() => _stream.Read(new byte[1], 0, 1));
  }
}
