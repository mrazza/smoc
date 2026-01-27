using Moq;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Views;
using TerminalGuiFluentTesting;

namespace smoc.Tests.Ui;

public class MainContentTest {
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly CommandService _commandService;
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public MainContentTest(ITestOutputHelper output) {
    _mockStreamingClient = new Mock<IStreamingClient>();
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _fakeMainWindow = new FakeMainWindow();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private MainContent NewMainContent() => new(_fakeMainWindow, _commandService, _mockPlaybackQueue.Object, _mockStreamingClient.Object);

  private static TerminalGuiFluentTesting.TestContext NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString()).ConfigureDefaultTheme();

  private TerminalGuiFluentTesting.TestContext NewMainContentContext() => NewContext().Add(NewMainContent());

  [Fact]
  public void InitialState_ShowsEmpty() {
    using var context = NewMainContentContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SetMode_Artist_ShowsArtist() {
    using var context = NewContext();
    var mainContent = NewMainContent();
    context.Add(mainContent).Then((_) => mainContent.SetMode(Mode.Artist));
    Assert.IsType<ArtistView>(mainContent.MostFocused?.SuperView ?? mainContent.MostFocused);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SetMode_Song_ShowsSong() {
    using var context = NewContext();
    var mainContent = NewMainContent();
    context.Add(mainContent).Then((_) => mainContent.SetMode(Mode.Song));
    Assert.IsType<SongView>(mainContent.MostFocused);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SetMode_Player_ShowsPlayer() {
    using var context = NewContext();
    var mainContent = NewMainContent();
    context.Add(mainContent).Then((_) => mainContent.SetMode(Mode.Player));
    Assert.IsType<PlayerView>(mainContent.MostFocused);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SetMode_ChangeMode_ShowsCorrectMode() {
    using var context = NewContext();
    var mainContent = NewMainContent();
    context.Add(mainContent).Then((_) => mainContent.SetMode(Mode.Player)).Then((_) => mainContent.SetMode(Mode.Artist));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SetMode_Playlist_ShowsPlaylist() {
    using var context = NewContext();
    var mainContent = NewMainContent();
    context.Add(mainContent).Then((_) => mainContent.SetMode(Mode.Playlist));
    Assert.IsType<PlaylistView>(mainContent.MostFocused?.SuperView ?? mainContent.MostFocused);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

}