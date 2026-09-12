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
  public async Task ConnectAsync_CallsClientConnectAndLaunch() {
    var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);

    await sut.ConnectAsync();

    _mockClient.Verify(c => c.ConnectChromecast(_device), Times.Once);
    _mockClient.Verify(c => c.LaunchApplicationAsync(It.IsAny<string>()), Times.Once);
  }

  [Fact]
  public void Volume_GetAndSet_UpdatesVolumeAndInvokesClient() {
    var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);

    sut.Volume = 0.8f;

    Assert.Equal(0.8f, sut.Volume);
    _mockClient.Verify(c => c.SetVolumeAsync(0.8f), Times.Once);
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

    _mockClient.Verify(c => c.DisconnectAsync(), Times.Once);
    _mockClient.Verify(c => c.Dispose(), Times.Once);
  }
}
