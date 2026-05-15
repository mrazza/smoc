using Moq;
using SharpCaster.Models;
using SharpCaster;
using Smoc.Services.Audio.Cast;
using Smoc.Services.Cast;
using Smoc.Streaming;
using smoc.Tests.TestInfra;

namespace smoc.Tests.Services.Audio.Cast;

public class CastAudioServiceTest {
    private readonly Mock<IStreamingProxyService> _mockProxyService;
    private readonly Chromecast _device;

    public CastAudioServiceTest() {
        _mockProxyService = new Mock<IStreamingProxyService>();
        _device = new Chromecast { 
            DeviceUri = new Uri("http://192.168.1.100:8008"),
            FriendlyName = "Test Cast Device"
        };
    }

    [Fact]
    public void MakePlaybackService_ReturnsCastPlaybackService() {
        var sut = new CastAudioService(_device, _mockProxyService.Object);
        var song = EntityTestFactory.GenerateSong();
        var stream = new MemoryStream();
        
        var playbackService = sut.MakePlaybackService(song, stream, "mp3");
        
        Assert.NotNull(playbackService);
        Assert.IsType<CastPlaybackService>(playbackService);
        Assert.Equal(song, playbackService.Song);
    }

    [Fact]
    public void MakePlaybackService_StartsProxyWithCorrectContentType() {
        var sut = new CastAudioService(_device, _mockProxyService.Object);
        var song = EntityTestFactory.GenerateSong();
        var stream = new MemoryStream();
        
        _mockProxyService.Setup(p => p.StartProxy(stream, "audio/mpeg")).Returns("http://proxy/stream");
        
        sut.MakePlaybackService(song, stream, "mp3");
        
        _mockProxyService.Verify(p => p.StartProxy(stream, "audio/mpeg"), Times.Once);
    }
}