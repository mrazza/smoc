using System.Net;
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

public class NowPlayingBarTest {
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly Mock<HttpClientHandler> _mockHttpClientHandler;
  private readonly HttpClient _httpClient;
  private readonly CommandService _commandService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public NowPlayingBarTest(ITestOutputHelper output) {
    _fakeMainWindow = new FakeMainWindow();
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
    _mockHttpClientHandler = new Mock<HttpClientHandler>();
    _httpClient = new HttpClient(_mockHttpClientHandler.Object);
  }

  private static TerminalGuiFluentTesting.TestContext NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());

  private NowPlayingBar NewNowPlaying() => new NowPlayingBar(_fakeMainWindow, _mockPlaybackQueue.Object, _commandService, _httpClient);

  private TerminalGuiFluentTesting.TestContext NewNowPlayingContext() => NewContext().AddAndLayout(NewNowPlaying());

  [Fact]
  public void PlayPauseHotKey_PlaysMusic() {
    using var context = NewContext();
    var nowPlaying = NewNowPlaying();
    _mockPlaybackQueue.Setup((ps) => ps.PlayPause()).Verifiable(Times.Once());
    context.Add(nowPlaying)
        .KeyDown(Key.Space);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void StopHotKey_StopsMusic() {
    using var context = NewContext();
    var nowPlaying = NewNowPlaying();
    _mockPlaybackQueue.Setup((ps) => ps.Stop()).Verifiable(Times.Once());
    context.Add(nowPlaying)
        .KeyDown(Key.Space.WithCtrl);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void NextSongHotKey_MovesNext() {
    using var context = NewContext();
    var nowPlaying = NewNowPlaying();
    _mockPlaybackQueue.Setup((ps) => ps.NextTrack()).Verifiable(Times.Once());
    context.Add(nowPlaying)
        .KeyDown(new Key('.'));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void PreviousSongHotKey_MovesPrevious() {
    using var context = NewContext();
    var nowPlaying = NewNowPlaying();
    _mockPlaybackQueue.Setup((ps) => ps.PreviousTrack(false, null)).Verifiable(Times.Once());
    context.Add(nowPlaying)
        .KeyDown(new Key(','));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void SeekForwardHotKey_SeeksForward() {
    using var context = NewContext();
    var nowPlaying = NewNowPlaying();
    _mockPlaybackQueue.Setup((ps) => ps.SeekForward(It.IsAny<TimeSpan>())).Verifiable(Times.Once());
    context.Add(nowPlaying)
        .KeyDown(new Key(']'));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void SeekBackwardHotKey_SeeksBackward() {
    using var context = NewContext();
    var nowPlaying = NewNowPlaying();
    _mockPlaybackQueue.Setup((ps) => ps.SeekBackward(It.IsAny<TimeSpan>())).Verifiable(Times.Once());
    context.Add(nowPlaying)
        .KeyDown(new Key('['));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void InitialState_ShowsEmpty() {
    using var context = NewNowPlayingContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void Volume_ShowsVolume() {
    _mockPlaybackQueue.SetupGet((ps) => ps.Volume).Returns(0.5f);
    using var context = NewNowPlayingContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void Volume_VolumeChanged_UpdatesUi() {
    EventHandler<float>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.VolumeChanged += It.IsAny<EventHandler<float>>())
        .Callback<EventHandler<float>>(h => handler = h);
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, 0.2f));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void Volume_VolumeCommand_SetsVolume() {
    _mockPlaybackQueue.SetupSet((ps) => ps.Volume = 0.2f).Verifiable(Times.Once());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/20"));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_NoArguments_DoesNothing() {
    _mockPlaybackQueue.SetupSet((ps) => ps.Volume = It.IsAny<float>()).Verifiable(Times.Never());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v"));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_TooLarge_DoesNothing() {
    _mockPlaybackQueue.SetupSet((ps) => ps.Volume = It.IsAny<float>()).Verifiable(Times.Never());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/200"));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_TooSmall_DoesNothing() {
    _mockPlaybackQueue.SetupSet((ps) => ps.Volume = It.IsAny<float>()).Verifiable(Times.Never());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/-10"));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_MultipleArgs_UsesFirst() {
    _mockPlaybackQueue.SetupSet((ps) => ps.Volume = 0.1f).Verifiable(Times.Once());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/10/20/30"));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void Volume_VolumeCommand_InvalidFormat_DoesNothing() {
    _mockPlaybackQueue.SetupSet((ps) => ps.Volume = It.IsAny<float>()).Verifiable(Times.Never());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/invalid"));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void OnSongChanged_UpdatesSongDetails() {
    var song = EntityTestFactory.GenerateSong();
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnSongChanged_SongIsNull_UpdatesSongDetails() {
    var song = EntityTestFactory.GenerateSong();
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, song))
        .Then((_) => handler?.Invoke(null, null));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnSongChanged_LoadsAlbumArt() {
    var song = EntityTestFactory.GenerateSong();
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
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
    var song = EntityTestFactory.GenerateSong();
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
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
    var song = EntityTestFactory.GenerateSong(noArt: true);
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    _mockHttpClientHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Verifiable(Times.Never());
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _mockHttpClientHandler.Verify();
  }

  [Fact]
  public void OnPositionChanged_UpdatesPosition() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>())
        .Callback<EventHandler<TimeSpan>>(h => handler = h);
    _mockPlaybackQueue.SetupGet((ps) => ps.Duration).Returns(TimeSpan.FromMinutes(5));
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, TimeSpan.Zero));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnPositionChanged_UpdatesPosition_MultipleTimes() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>())
        .Callback<EventHandler<TimeSpan>>(h => handler = h);
    _mockPlaybackQueue.SetupGet((ps) => ps.Duration).Returns(TimeSpan.FromMinutes(5));
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, TimeSpan.Zero))
        .Then((_) => handler?.Invoke(null, TimeSpan.FromMinutes(2)));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  private static byte[] GetImageBytes() {
    return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mP8z8BQz0AEYBxVSF+FABJADveWkH6oAAAAAElFTkSuQmCC");
  }
}