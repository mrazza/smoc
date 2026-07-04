using System.Net;
using Moq;
using Moq.Protected;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace smoc.Tests.Ui;

public class NowPlayingBarTest {
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly CommandService _commandService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public NowPlayingBarTest(ITestOutputHelper output) {
    _fakeMainWindow = new FakeMainWindow();
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
    _mockStreamingClient = new Mock<IStreamingClient>();
  }

  private static AppTestHelper NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());

  private NowPlayingBar NewNowPlaying() => new(_fakeMainWindow, _mockPlaybackQueue.Object, _commandService, _mockStreamingClient.Object);

  private AppTestHelper NewNowPlayingContext() => NewContext().AddAndLayout(NewNowPlaying());

  [Fact]
  public void PlayPauseHotKey_PlaysMusic() {
    using var context = NewNowPlayingContext();
    _mockPlaybackQueue.Setup((ps) => ps.PlayPause()).Verifiable(Times.Once());
    context.KeyDown(Key.Space);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void StopHotKey_StopsMusic() {
    using var context = NewNowPlayingContext();
    _mockPlaybackQueue.Setup((ps) => ps.Stop()).Verifiable(Times.Once());
    context.KeyDown(Key.Space.WithCtrl);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void NextSongHotKey_MovesNext() {
    using var context = NewNowPlayingContext();
    _mockPlaybackQueue.Setup((ps) => ps.NextTrack()).Verifiable(Times.Once());
    context.KeyDown(new Key('.'));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void PreviousSongHotKey_MovesPrevious() {
    using var context = NewNowPlayingContext();
    _mockPlaybackQueue.Setup((ps) => ps.PreviousTrack(false, null)).Verifiable(Times.Once());
    context.KeyDown(new Key(','));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void SeekForwardHotKey_SeeksForward() {
    using var context = NewNowPlayingContext();
    _mockPlaybackQueue.Setup((ps) => ps.SeekForward(It.IsAny<TimeSpan>())).Verifiable(Times.Once());
    context.KeyDown(new Key(']'));
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void SeekBackwardHotKey_SeeksBackward() {
    using var context = NewNowPlayingContext();
    _mockPlaybackQueue.Setup((ps) => ps.SeekBackward(It.IsAny<TimeSpan>())).Verifiable(Times.Once());
    context.KeyDown(new Key('['));
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

  /// <summary>
  /// Tests that setting a volume beyond 200% does nothing.
  /// </summary>
  [Fact]
  public void Volume_VolumeCommand_TooLarge_DoesNothing() {
    _mockPlaybackQueue.SetupSet((ps) => ps.Volume = It.IsAny<float>()).Verifiable(Times.Never());
    using var context = NewNowPlayingContext()
        .Then((_) => _commandService.ExecuteCommand("v/201"));
    _mockPlaybackQueue.Verify();
  }

  /// <summary>
  /// Tests that the volume command sets the volume correctly to 200%.
  /// </summary>
  [Fact]
  public void Volume_VolumeCommand_SetsVolumeToMax() {
    _mockPlaybackQueue.SetupSet((ps) => ps.Volume = 2.0f).Verifiable(Times.Once());
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
    using var imageStream = GetImage();
    _mockStreamingClient.Setup(client => client.GetAlbumArtAsync(song.Album, It.IsAny<Func<IEnumerable<AlbumCover>, AlbumCover>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(imageStream);
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _mockStreamingClient.Verify();
  }

  [Fact]
  public void OnSongChanged_RepeatAlbum_CachesAlbumArt() {
    var song = EntityTestFactory.GenerateSong();
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    using var imageStream = GetImage();
    _mockStreamingClient.Setup(client => client.GetAlbumArtAsync(song.Album, It.IsAny<Func<IEnumerable<AlbumCover>, AlbumCover>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(imageStream).Verifiable(Times.Once());
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, song))
        .Then((_) => handler?.Invoke(null, song));
    _mockStreamingClient.Verify();
  }

  [Fact]
  public void OnSongChanged_NoAlbumArt_ClearsAlbumArt() {
    var song = EntityTestFactory.GenerateSong(noArt: true);
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    _mockStreamingClient.Setup(client => client.GetAlbumArtAsync(song.Album, It.IsAny<Func<IEnumerable<AlbumCover>, AlbumCover>>(), It.IsAny<CancellationToken>()))
        .Verifiable(Times.Never());
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _mockStreamingClient.Verify();
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

  private static Image<Rgba32> GetImage() {
    return new Image<Rgba32>(10, 10);
  }
}