using Moq;
using Sharpcaster.Models.Media;
using Smoc.Services.Audio.Cast;
using Smoc.Services.Cast;
using Smoc.Streaming;
using smoc.Tests.TestInfra;

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
    public void Play_Stopped_CallsLoadAsync() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Play();
        
        _mockClient.Verify(c => c.LoadAsync(It.Is<Media>(m => m.ContentUrl == _url)), Times.Once);
        Assert.Equal(Smoc.Services.PlaybackState.Playing, sut.PlaybackState);
    }

    [Fact]
    public void Play_Paused_CallsPlayAsync() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        sut.Play(); // Set to Playing first
        sut.Pause(); // Set to Paused
        
        sut.Play();
        
        _mockClient.Verify(c => c.PlayAsync(), Times.Once);
        Assert.Equal(Smoc.Services.PlaybackState.Playing, sut.PlaybackState);
    }

    [Fact]
    public void Pause_CallsPauseAsync() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Pause();
        
        _mockClient.Verify(c => c.PauseAsync(), Times.Once);
        Assert.Equal(Smoc.Services.PlaybackState.Paused, sut.PlaybackState);
    }

    [Fact]
    public void Stop_CallsStopAsync() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Stop();
        
        _mockClient.Verify(c => c.StopAsync(), Times.Once);
        Assert.Equal(Smoc.Services.PlaybackState.Stopped, sut.PlaybackState);
    }

    [Fact]
    public void Seek_CallsSeekAsync() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        var position = TimeSpan.FromSeconds(30);
        
        sut.Seek(position);
        
        _mockClient.Verify(c => c.SeekAsync(30.0), Times.Once);
    }

    [Fact]
    public void Dispose_StopsProxyAndDisposesStream() {
        var sut = new CastPlaybackService(_mockClient.Object, _song, _stream, _url, _mockProxyService.Object);
        
        sut.Dispose();
        
        _mockProxyService.Verify(p => p.StopProxy(), Times.Once);
        Assert.Throws<ObjectDisposedException>(() => _stream.Read(new byte[1], 0, 1));
    }
}