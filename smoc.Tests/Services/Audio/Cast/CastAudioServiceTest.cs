using System.Collections.Generic;
using Smoc.Services;
using Sharpcaster.Models.Media;
using Moq;
using Sharpcaster.Models;
using Smoc.Services.Audio.Cast;
using Smoc.Services.Cast;
using Smoc.Streaming;
using smoc.Tests.TestInfra;
using System;
using System.IO;
using System.Threading.Tasks;

namespace smoc.Tests.Services.Audio.Cast;

public class CastAudioServiceTest {
  private readonly Mock<IStreamingProxyService> _mockProxyService;
  private readonly Mock<IChromecastClient> _mockClient;
  private readonly ChromecastReceiver _device;

  public CastAudioServiceTest() {
    _mockProxyService = new Mock<IStreamingProxyService>();
    _mockClient = new Mock<IChromecastClient>();
    _device = new ChromecastReceiver {
      DeviceUri = new Uri("http://192.168.1.100:8008"),
      Name = "Test Cast Device"
    };
  }

  [Fact]
  public void MakePlaybackService_ReturnsCastPlaybackService() {
    var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);
    var song = EntityTestFactory.GenerateSong();
    var stream = new MemoryStream();

    var playbackService = sut.MakePlaybackService(song, stream, "mp3", TestContext.Current.CancellationToken);

    Assert.NotNull(playbackService);
    Assert.IsType<CastPlaybackService>(playbackService);
    Assert.Equal(song, playbackService.Song);
  }

  [Fact]
  public void MakePlaybackService_StartsProxyWithCorrectContentType() {
    var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);
    var song = EntityTestFactory.GenerateSong();
    var stream = new MemoryStream();

    _mockProxyService.Setup(p => p.StartProxy(stream, "audio/mpeg")).Returns("http://proxy/stream");

    sut.MakePlaybackService(song, stream, "mp3", TestContext.Current.CancellationToken);

    _mockProxyService.Verify(p => p.StartProxy(stream, "audio/mpeg"), Times.Once);
  }

  [Fact]
  public async Task ConnectAsync_CallsClientEnsureConnectedAndLaunched() {
    var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);

    await sut.ConnectAsync(TestContext.Current.CancellationToken);

    _mockClient.Verify(c => c.EnsureConnectedAndLaunchedAsync(_device, "CC1AD845", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public void Volume_GetAndSet_UpdatesVolumeAndInvokesClient() {
    var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);

    sut.Volume = 0.8f;

    Assert.Equal(0.8f, sut.Volume);
    _mockClient.Verify(c => c.SetVolumeAsync(0.8f, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Theory]
  [InlineData("flac", "audio/flac")]
  [InlineData("m4a", "audio/mp4")]
  [InlineData("aac", "audio/aac")]
  [InlineData("ogg", "audio/ogg")]
  [InlineData("wav", "audio/wav")]
  [InlineData("unknown", "audio/mpeg")]
  public void MakePlaybackService_MapsCodecsToCorrectMimeType(string codec, string expectedMimeType) {
    var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);
    var song = EntityTestFactory.GenerateSong();
    var stream = new MemoryStream();

    _mockProxyService.Setup(p => p.StartProxy(stream, expectedMimeType)).Returns("http://proxy/stream");

    sut.MakePlaybackService(song, stream, codec, TestContext.Current.CancellationToken);

    _mockProxyService.Verify(p => p.StartProxy(stream, expectedMimeType), Times.Once);
  }

  [Fact]
  public void Dispose_DisconnectsAndDisposesClient() {
    var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);

    sut.Dispose();

    _mockClient.Verify(c => c.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    _mockClient.Verify(c => c.Dispose(), Times.Once);
  }

  [Fact]
  public async Task OnSongEnded_AutomaticallyTransitionsToNextPreloadedTrack() {
    var loadedMediaTitles = new List<string>();
    _mockClient.Setup(c => c.LoadAsync(It.IsAny<Media>(), It.IsAny<CancellationToken>())).Returns((Media m, CancellationToken ct) => {
      loadedMediaTitles.Add(m.Metadata?.Title ?? string.Empty);
      return Task.CompletedTask;
    });

    var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);
    var mockStreaming = new Mock<IStreamingClient>();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var song2 = EntityTestFactory.GenerateSong(id: "2", postfix: "2");

    mockStreaming.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "mp3", new MemoryStream()));
    mockStreaming.Setup(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song2.Id, "mp3", new MemoryStream()));

    _mockProxyService.Setup(p => p.StartProxy(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
      .Returns((Stream s, string c, string h) => $"http://proxy/{Guid.NewGuid()}");

    var fakeWindow = new smoc.Tests.Fakes.FakeMainWindow();
    using var queue = new StandardPlaybackQueueService(fakeWindow, mockStreaming.Object, sut);

    queue.QueueNext(new[] { song1, song2 });
    await queue.Play();

    // Allow preloading task to complete
    await Task.Delay(100, TestContext.Current.CancellationToken);

    Assert.Equal(song1, queue.CurrentSong);
    Assert.Equal(PlaybackState.Playing, queue.PlaybackState);
    Assert.Equal(new[] { song1.Title }, loadedMediaTitles);

    // Simulate Chromecast finishing song 1
    _mockClient.Raise(c => c.MediaStatusChanged += null, _mockClient.Object, new MediaStatus {
      PlayerState = PlayerStateType.Idle,
      IdleReason = "FINISHED"
    });

    // Allow track transition to complete
    await Task.Delay(200, TestContext.Current.CancellationToken);

    Assert.Equal(song2, queue.CurrentSong);
    Assert.Equal(PlaybackState.Playing, queue.PlaybackState);
    Assert.Equal(new[] { song1.Title, song2.Title }, loadedMediaTitles);
    _mockClient.Verify(c => c.StopAsync(), Times.Never);
  }
}
