using Moq;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Services.Audio;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using StatusBar = Smoc.Ui.StatusBar;

namespace smoc.Tests.Ui;

/// <summary>
/// Unit tests for <see cref="MainWindow"/>.
/// </summary>
public class MainWindowTest {
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly Mock<ISessionStateService> _mockSessionStateService;
  private readonly Mock<IAudioService> _mockAudioService;

  /// <summary>
  /// Initializes a new instance of <see cref="MainWindowTest"/>.
  /// </summary>
  public MainWindowTest() {
    _mockStreamingClient = new Mock<IStreamingClient>();
    _mockSessionStateService = new Mock<ISessionStateService>();
    _mockAudioService = new Mock<IAudioService>();

    _mockAudioService.Setup(a => a.GetPlaybackDevices()).Returns([
      new AudioDeviceInfo(0, "Default Speakers", true, true),
      new AudioDeviceInfo(1, "USB DAC", false, false)
    ]);
  }

  private MainWindow NewMainWindow() =>
    new MainWindow(_mockStreamingClient.Object, _mockSessionStateService.Object, _mockAudioService.Object);

  private static AppTestHelper NewContext() =>
    With.A<Runnable>(100, 20, TestDriver.ANSI.ToString()).ConfigureDefaultTheme();

  /// <summary>
  /// Verifies that pressing ':' transitions mode to Command.
  /// </summary>
  [Fact]
  public void HotKey_Colon_SetsModeCommand() {
    using var context = NewContext();
    var mainWindow = NewMainWindow();
    context.Add(mainWindow);

    context.KeyDown(new Key(':'));

    var statusBar = mainWindow.SubViews.OfType<StatusBar>().First();
    var commandLine = mainWindow.SubViews.OfType<CommandLine>().First();
    Assert.Equal("COMMAND", statusBar.GetMode());
    Assert.Equal(CommandLineMode.Command, commandLine.InputMode);
  }

  /// <summary>
  /// Verifies that pressing '/' transitions mode to Filter.
  /// </summary>
  [Fact]
  public void HotKey_Slash_SetsModeFilter() {
    using var context = NewContext();
    var mainWindow = NewMainWindow();
    context.Add(mainWindow);

    context.KeyDown(new Key('/'));

    var statusBar = mainWindow.SubViews.OfType<StatusBar>().First();
    var commandLine = mainWindow.SubViews.OfType<CommandLine>().First();
    Assert.Equal("FILTER", statusBar.GetMode());
    Assert.Equal(CommandLineMode.Filter, commandLine.InputMode);
  }

  /// <summary>
  /// Verifies that the :device command with a device name selects the matching device.
  /// </summary>
  [Fact]
  public void DeviceCommand_ExactNameMatch_SelectsDevice() {
    _mockAudioService.Setup(a => a.SelectPlaybackDevice(It.Is<AudioDeviceInfo>(d => d.Index == 1))).Returns(true);

    using var context = NewContext();
    var mainWindow = NewMainWindow();
    context.Add(mainWindow);

    context.KeyDown(new Key(':'))
        .KeyDown(Key.D).KeyDown(Key.E).KeyDown(Key.V).KeyDown(Key.I).KeyDown(Key.C).KeyDown(Key.E)
        .KeyDown(new Key('/'))
        .KeyDown(Key.U).KeyDown(Key.S).KeyDown(Key.B)
        .KeyDown(Key.Space)
        .KeyDown(Key.D).KeyDown(Key.A).KeyDown(Key.C)
        .KeyDown(Key.Enter);

    _mockAudioService.Verify(a => a.SelectPlaybackDevice(It.Is<AudioDeviceInfo>(d => d.Index == 1)), Times.Once());
  }

  /// <summary>
  /// Verifies that the :device command with a device index selects the matching device.
  /// </summary>
  [Fact]
  public void DeviceCommand_IndexMatch_SelectsDevice() {
    _mockAudioService.Setup(a => a.SelectPlaybackDevice(It.Is<AudioDeviceInfo>(d => d.Index == 1))).Returns(true);

    using var context = NewContext();
    var mainWindow = NewMainWindow();
    context.Add(mainWindow);

    context.KeyDown(new Key(':'))
        .KeyDown(Key.D).KeyDown(Key.E).KeyDown(Key.V).KeyDown(Key.I).KeyDown(Key.C).KeyDown(Key.E)
        .KeyDown(new Key('/'))
        .KeyDown(Key.D1)
        .KeyDown(Key.Enter);

    _mockAudioService.Verify(a => a.SelectPlaybackDevice(It.Is<AudioDeviceInfo>(d => d.Index == 1)), Times.Once());
  }
}
