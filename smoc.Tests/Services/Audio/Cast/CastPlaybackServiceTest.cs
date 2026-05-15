using Moq;
using Sharpcaster.Models.Media;
using Smoc.Services.Audio.Cast;
using Smoc.Services.Cast;
using Smoc.Streaming;
using smoc.Tests.TestInfra;
using System;
using System.IO;
using System.Threading.Tasks;

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
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        
        Assert.Equal(Smoc.Services.PlaybackState.Stopped, sut.PlaybackState);
        Assert.Equal(_song, sut.Song);
        Assert.Equal(TimeSpan.Zero, sut.CurrentTime);
    }

    [Fact]
    public void Play_UpdatesStateToPlaying() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Play();
        
        Assert.Equal(Smoc.Services.PlaybackState.Playing, sut.PlaybackState);
        _mockClient.Verify(c => c.LoadAsync(It.IsAny<Media>()), Times.Once);
    }

    [Fact]
    public void Pause_UpdatesStateToPaused() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Play();
        sut.Pause();
        
        Assert.Equal(Smoc.Services.PlaybackState.Paused, sut.PlaybackState);
        _mockClient.Verify(c => c.PauseAsync(), Times.Once);
    }

    [Fact]
    public void Stop_UpdatesStateToStopped() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Play();
        sut.Stop();
        
        Assert.Equal(Smoc.Services.PlaybackState.Stopped, sut.PlaybackState);
        _mockClient.Verify(c => c.StopAsync(), Times.Once);
    }

    [Fact]
    public void Dispose_StopsProxyAndDisposesStream() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Dispose();
        
        _mockProxyService.Verify(p => p.StopProxy(), Times.Once);
        Assert.Throws<ObjectDisposedException>(() => _stream.Read(new byte[1], 0, 1));
    }
}