using Moq;
using Sharpcaster.Models;
using Smoc.Services.Audio.Cast;
using Smoc.Services.Cast;
using Smoc.Streaming;
using smoc.Tests.TestInfra;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace smoc.Tests.Services.Audio.Cast;

public class CastAudioServiceTest {
    private readonly Mock<IStreamingProxyService> _mockProxyService;
    private readonly Mock<IChromecastClient> _mockClient;
    private readonly Sharpcaster.Models.ChromecastReceiver _device;

    public CastAudioServiceTest() {
        _mockProxyService = new Mock<IStreamingProxyService>();
        _mockClient = new Mock<IChromecastClient>();
        _device = new Sharpcaster.Models.ChromecastReceiver { 
            DeviceUri = new Uri("http://192.168.1.100:8008"),
            Name = "Test Cast Device"
        };
    }

    [Fact]
    public void MakePlaybackService_ReturnsCastPlaybackService() {
        var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);
        var song = EntityTestFactory.GenerateSong();
        var stream = new MemoryStream();
        
        var playbackService = sut.MakePlaybackService(song, stream, "mp3");
        
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
        
        sut.MakePlaybackService(song, stream, "mp3");
        
        _mockProxyService.Verify(p => p.StartProxy(stream, "audio/mpeg"), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_CallsClientConnectAndLaunch() {
        var sut = new CastAudioService(_device, _mockProxyService.Object, _mockClient.Object);
        
        await sut.ConnectAsync();
        
        _mockClient.Verify(c => c.ConnectChromecast(_device), Times.Once);
        _mockClient.Verify(c => c.LaunchApplicationAsync(It.IsAny<string>()), Times.Once);
    }
}