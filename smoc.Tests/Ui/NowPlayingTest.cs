using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Moq;
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
  private readonly CommandService _commandService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public NowPlayingTest(ITestOutputHelper output) {
    _fakeMainWindow = new FakeMainWindow();
    _mockPlayerService = new Mock<IPlayerService>();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private TerminalGuiFluentTesting.TestContext NewContext() {
    return With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());
  }

  private NowPlaying NewNowPlaying() {
    return new NowPlaying(_fakeMainWindow, _mockPlayerService.Object, _commandService);
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
}