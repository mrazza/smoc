using Moq;
using SharpCaster;
using SharpCaster.Models.MediaStatus;
using Smoc.Services.Audio.Cast;
using Smoc.Services.Cast;
using Smoc.Streaming;
using smoc.Tests.TestInfra;

namespace smoc.Tests.Services.Audio.Cast;

public class CastPlaybackServiceTest {
    private readonly Mock<IStreamingProxyService> _mockProxyService;
    private readonly Song _song;
    private readonly MemoryStream _stream;
    private readonly string _url = "http://proxy/stream";
    // Note: ChromeCastClient might be hard to mock if it doesn't have an interface.
    // In a real scenario, we might need to wrap it.
    // For now, let's see if we can at least test the state management.

    public CastPlaybackServiceTest() {
        _mockProxyService = new Mock<IStreamingProxyService>();
        _song = EntityTestFactory.GenerateSong();
        _stream = new MemoryStream();
    }

    [Fact]
    public void InitialState_IsStopped() {
        var client = new SharpCaster.ChromeCastClient();
        var sut = new CastPlaybackService(client, _song, _stream, _url, _mockProxyService.Object);
        
        Assert.Equal(Smoc.Services.PlaybackState.Stopped, sut.PlaybackState);
        Assert.Equal(_song, sut.Song);
        Assert.Equal(TimeSpan.Zero, sut.CurrentTime);
    }

    [Fact]
    public void Play_UpdatesStateToPlaying() {
        var client = new SharpCaster.ChromeCastClient();
        var sut = new CastPlaybackService(client, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Play();
        
        Assert.Equal(Smoc.Services.PlaybackState.Playing, sut.PlaybackState);
    }

    [Fact]
    public void Pause_UpdatesStateToPaused() {
        var client = new SharpCaster.ChromeCastClient();
        var sut = new CastPlaybackService(client, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Play();
        sut.Pause();
        
        Assert.Equal(Smoc.Services.PlaybackState.Paused, sut.PlaybackState);
    }

    [Fact]
    public void Stop_UpdatesStateToStopped() {
        var client = new SharpCaster.ChromeCastClient();
        var sut = new CastPlaybackService(client, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Play();
        sut.Stop();
        
        Assert.Equal(Smoc.Services.PlaybackState.Stopped, sut.PlaybackState);
    }

    [Fact]
    public void Dispose_StopsProxyAndDisposesStream() {
        var client = new SharpCaster.ChromeCastClient();
        var sut = new CastPlaybackService(client, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Dispose();
        
        _mockProxyService.Verify(p => p.StopProxy(), Times.Once);
        Assert.Throws<ObjectDisposedException>(() => _stream.Read(new byte[1], 0, 1));
    }
}