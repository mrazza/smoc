using System.Buffers.Text;
using System.Net;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Moq;
using Moq.Protected;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using TerminalGuiFluentTesting;

namespace smoc.Tests.Ui;

public class NowPlayingTest {
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IPlayerService> _mockPlayerService;
  private readonly Mock<HttpClientHandler> _mockHttpClientHandler;
  private readonly HttpClient _httpClient;
  private readonly CommandService _commandService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public NowPlayingTest(ITestOutputHelper output) {
    _fakeMainWindow = new FakeMainWindow();
    _mockPlayerService = new Mock<IPlayerService>();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
    _mockHttpClientHandler = new Mock<HttpClientHandler>();
    _httpClient = new HttpClient(_mockHttpClientHandler.Object);
  }

  private TerminalGuiFluentTesting.TestContext NewContext() {
    return With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());
  }

  private NowPlaying NewNowPlaying() {
    return new NowPlaying(_fakeMainWindow, _mockPlayerService.Object, _commandService, _httpClient);
  }

  private TerminalGuiFluentTesting.TestContext NewNowPlayingContext() {
    return NewContext().Add(NewNowPlaying());
  }

  [Fact]
  public void PlayPauseHotKey_PlaysMusic() {
    using var context = NewContext();
    var nowPlaying = NewNowPlaying();
    _mockPlayerService.Setup((ps) => ps.PlayPause()).Verifiable(Times.Once());
    context.Add(nowPlaying)
        .KeyDown(Key.Space);
    _mockPlayerService.Verify();
  }

  [Fact]
  public void InitialState_ShowsEmpty() {
    using var context = NewNowPlayingContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void Volume_ShowsVolume() {
    _mockPlayerService.SetupGet((ps) => ps.Volume).Returns(0.5f);
    using var context = NewNowPlayingContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void Volume_VolumeChanged_UpdatesUi() {
    EventHandler<float>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.VolumeChanged += It.IsAny<EventHandler<float>>())
        .Callback<EventHandler<float>>(h => handler = h);
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, 0.2f));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void Volume_VolumeCommand_SetsVolume() {
    _mockPlayerService.SetupSet((ps) => ps.Volume = 0.2f).Verifiable(Times.Once());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/20"));
    _mockPlayerService.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_NoArguments_DoesNothing() {
    _mockPlayerService.SetupSet((ps) => ps.Volume = It.IsAny<float>()).Verifiable(Times.Never());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v"));
    _mockPlayerService.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_TooLarge_DoesNothing() {
    _mockPlayerService.SetupSet((ps) => ps.Volume = It.IsAny<float>()).Verifiable(Times.Never());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/200"));
    _mockPlayerService.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_TooSmall_DoesNothing() {
    _mockPlayerService.SetupSet((ps) => ps.Volume = It.IsAny<float>()).Verifiable(Times.Never());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/-10"));
    _mockPlayerService.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_MultipleArgs_UsesFirst() {
    _mockPlayerService.SetupSet((ps) => ps.Volume = 0.1f).Verifiable(Times.Once());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/10/20/30"));
    _mockPlayerService.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_InvalidFormat_DoesNothing() {
    _mockPlayerService.SetupSet((ps) => ps.Volume = It.IsAny<float>()).Verifiable(Times.Never());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/invalid"));
    _mockPlayerService.Verify();
  }

  [Fact]
  public void OnSongChanged_UpdatesSongDetails() {
    var song = MakeSong();
    EventHandler<Song>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song>>())
        .Callback<EventHandler<Song>>(h => handler = h);
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnSongChanged_LoadsAlbumArt() {
    var song = MakeSong();
    EventHandler<Song>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song>>())
        .Callback<EventHandler<Song>>(h => handler = h);
    _mockHttpClientHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage {
                  StatusCode = HttpStatusCode.OK,
                  Content = new ByteArrayContent(GetImageBytes())
                }).Verifiable(Times.Once());
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _mockHttpClientHandler.Verify();
  }

  [Fact]
  public void OnSongChanged_RepeatAlbum_CachesAlbumArt() {
    var song = MakeSong();
    EventHandler<Song>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song>>())
        .Callback<EventHandler<Song>>(h => handler = h);
    _mockHttpClientHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage {
                  StatusCode = HttpStatusCode.OK,
                  Content = new ByteArrayContent(GetImageBytes())
                }).Verifiable(Times.Once());
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, song))
        .Then((_) => handler?.Invoke(null, song));
    _mockHttpClientHandler.Verify();
  }

  [Fact]
  public void OnSongChanged_NoAlbumArt_ClearsAlbumArt() {
    var song = MakeSong(noArt: true);
    EventHandler<Song>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song>>())
        .Callback<EventHandler<Song>>(h => handler = h);
    _mockHttpClientHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Verifiable(Times.Never());
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _mockHttpClientHandler.Verify();
  }

  [Fact]
  public void OnPositionChanged_UpdatesPosition() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>())
        .Callback<EventHandler<TimeSpan>>(h => handler = h);
    _mockPlayerService.SetupGet((ps) => ps.Duration).Returns(TimeSpan.FromMinutes(5));
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, TimeSpan.Zero));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnPositionChanged_UpdatesPosition_MultipleTimes() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>())
        .Callback<EventHandler<TimeSpan>>(h => handler = h);
    _mockPlayerService.SetupGet((ps) => ps.Duration).Returns(TimeSpan.FromMinutes(5));
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, TimeSpan.Zero))
        .Then((_) => handler?.Invoke(null, TimeSpan.FromMinutes(2)));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  private static Song MakeSong(bool noArt = false) {
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, noArt ? null : "http://url.com/thumb.png");
    return new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
  }

  private static byte[] GetImageBytes() {
    return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mP8z8BQz0AEYBxVSF+FABJADveWkH6oAAAAAElFTkSuQmCC");
  }
}