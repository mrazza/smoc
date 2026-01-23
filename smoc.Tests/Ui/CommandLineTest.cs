using smoc.Tests.TestInfra;
using Smoc.Ui;
using Terminal.Gui.Configuration;
using Terminal.Gui.Input;
using Terminal.Gui.Time;
using Terminal.Gui.Views;
using TerminalGuiFluentTesting;

namespace smoc.Tests.Ui;

public class CommandLineTest {

  private readonly ScreenshotDiffer _screenshotDiffer;

  public CommandLineTest(ITestOutputHelper output) {
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private static CommandLine NewCommandLine() => new();

  private static TerminalGuiFluentTesting.TestContext NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString()).ConfigureDefaultTheme();

  private static TerminalGuiFluentTesting.TestContext NewCommandLineContext() => NewContext().Add(NewCommandLine());

  [Fact]
  public void DisplayError_ShowsError() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    context.Add(commandLine)
        .Then((_) => commandLine.DisplayError("Error message 123"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void DisplayError_ClearsAfterTimeout() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    context.Add(commandLine)
        .Then((_) => commandLine.DisplayError("Error message 123"))
        .AdvanceTime(TimeSpan.FromSeconds(6));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnFocus_ClearsError() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    var button = new Button();
    context.Add(commandLine).Add(button);
    Assert.False(commandLine.HasFocus);
    context
        .Then((_) => commandLine.DisplayError("Error message 123"))
        .Then((_) => commandLine.SetFocus())
        .Then((_) => button.Visible = false);
    Assert.True(commandLine.HasFocus);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void WhenFocused_AcceptsText_ShowsPrefix() {
    using var context = NewCommandLineContext();
    context.KeyDown(Key.H).KeyDown(Key.E).KeyDown(Key.L).KeyDown(Key.L).KeyDown(Key.O);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void LosesFocus_ClearsText() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    var button = new Button();
    context.Add(button).Add(commandLine);
    Assert.True(commandLine.HasFocus);
    context
        .KeyDown(Key.H)
        .KeyDown(Key.E)
        .KeyDown(Key.L)
        .KeyDown(Key.L)
        .KeyDown(Key.O)
        .Then((_) => button.SetFocus());
    Assert.False(commandLine.HasFocus);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void EscapePressed_CancelCommand() {
    bool wasCanceled = false;
    using var context = NewContext();
    var commandLine = NewCommandLine();
    commandLine.CommandCancelled += (_, __) => wasCanceled = true;
    context.Add(commandLine);
    Assert.False(wasCanceled);
    context.KeyDown(Key.Esc);
    Assert.True(wasCanceled);
  }

  [Fact]
  public void EmptyText_CancelCommand() {
    bool wasCanceled = false;
    using var context = NewContext();
    var commandLine = NewCommandLine();
    commandLine.CommandCancelled += (_, __) => wasCanceled = true;
    context.Add(commandLine);
    Assert.False(wasCanceled);
    context.KeyDown(Key.Backspace);
    Assert.True(wasCanceled);
  }

  [Fact]
  public void EnterPressed_SubmitCommand() {
    bool wasSubmitted = false;
    using var context = NewContext();
    var commandLine = NewCommandLine();
    commandLine.Accepted += (_, __) => wasSubmitted = true;
    context.Add(commandLine);
    Assert.False(wasSubmitted);
    context.KeyDown(Key.Enter);
    Assert.True(wasSubmitted);
  }

  [Fact]
  public void TabPressed_DoesNotAdvanceFocus() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    var button = new Button();
    context.Add(commandLine).Add(button);
    Assert.False(commandLine.HasFocus);
    context.Then((_) => commandLine.SetFocus());
    Assert.True(commandLine.HasFocus);
    context.KeyDown(Key.Tab);
    Assert.True(commandLine.HasFocus);
  }

}