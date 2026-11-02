using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace smoc.Tests.Ui;

public class CommandLineTest {

  private readonly ScreenshotDiffer _screenshotDiffer;

  public CommandLineTest(ITestOutputHelper output) {
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private static CommandLine NewCommandLine() => new();

  private static AppTestHelper NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString()).ConfigureDefaultTheme();

  private static AppTestHelper NewCommandLineContext() => NewContext().Add(NewCommandLine());

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

  [Fact]
  public void History_AcceptedCommands_AddedToHistoryWithoutConsecutiveDuplicates() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    context.Add(commandLine);

    context.KeyDown(Key.P).KeyDown(Key.L).KeyDown(Key.A).KeyDown(Key.Y).KeyDown(Key.Enter);
    Assert.Equal(["play"], commandLine.History);

    commandLine.StartInput(CommandLineMode.Command);
    context.KeyDown(Key.P).KeyDown(Key.A).KeyDown(Key.U).KeyDown(Key.S).KeyDown(Key.E).KeyDown(Key.Enter);
    Assert.Equal(["play", "pause"], commandLine.History);

    // Consecutive duplicate should not be added
    commandLine.StartInput(CommandLineMode.Command);
    context.KeyDown(Key.P).KeyDown(Key.A).KeyDown(Key.U).KeyDown(Key.S).KeyDown(Key.E).KeyDown(Key.Enter);
    Assert.Equal(["play", "pause"], commandLine.History);

    // Empty command should not be added
    commandLine.StartInput(CommandLineMode.Command);
    context.KeyDown(Key.Enter);
    Assert.Equal(["play", "pause"], commandLine.History);
  }

  [Fact]
  public void History_CursorUpAndDown_NavigatesAndRestoresDraft() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    context.Add(commandLine);

    context.KeyDown(Key.A).KeyDown(Key.Enter);
    commandLine.StartInput(CommandLineMode.Command);
    context.KeyDown(Key.B).KeyDown(Key.Enter);

    // Type a draft
    commandLine.StartInput(CommandLineMode.Command);
    context.KeyDown(Key.D).KeyDown(Key.R);
    Assert.Equal(":dr", commandLine.Text);

    // Up arrow loads latest history item
    context.KeyDown(Key.CursorUp);
    Assert.Equal(":b", commandLine.Text);

    // Up arrow again loads older item
    context.KeyDown(Key.CursorUp);
    Assert.Equal(":a", commandLine.Text);

    // Up arrow at oldest stays at oldest
    context.KeyDown(Key.CursorUp);
    Assert.Equal(":a", commandLine.Text);

    // Down arrow moves forward
    context.KeyDown(Key.CursorDown);
    Assert.Equal(":b", commandLine.Text);

    // Down arrow past latest restores draft
    context.KeyDown(Key.CursorDown);
    Assert.Equal(":dr", commandLine.Text);

    // Down arrow again stays on draft
    context.KeyDown(Key.CursorDown);
    Assert.Equal(":dr", commandLine.Text);
  }

  [Fact]
  public void History_CursorUp_EmptyHistory_DoesNotThrow() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    context.Add(commandLine);

    context.KeyDown(Key.CursorUp);
    Assert.Equal(":", commandLine.Text);

    context.KeyDown(Key.CursorDown);
    Assert.Equal(":", commandLine.Text);
  }

  [Fact]
  public void TabCompletion_CyclesAlphabetically() {
    var commandService = new CommandService();
    commandService.RegisterCommand("play", (_, __) => { });
    commandService.RegisterCommand("pause", (_, __) => { });
    commandService.RegisterCommand("playlist", (_, __) => { });
    commandService.RegisterCommand("stop", (_, __) => { });

    using var context = NewContext();
    var commandLine = new CommandLine(commandService);
    context.Add(commandLine);

    // Start with 'p'
    context.KeyDown(Key.P);
    Assert.Equal(":p", commandLine.Text);

    // Tab 1 -> pause (first alphabetically among pause, play, playlist)
    context.KeyDown(Key.Tab);
    Assert.Equal(":pause", commandLine.Text);

    // Tab 2 -> play
    context.KeyDown(Key.Tab);
    Assert.Equal(":play", commandLine.Text);

    // Tab 3 -> playlist
    context.KeyDown(Key.Tab);
    Assert.Equal(":playlist", commandLine.Text);

    // Tab 4 wraps around -> pause
    context.KeyDown(Key.Tab);
    Assert.Equal(":pause", commandLine.Text);
  }

  [Fact]
  public void TabCompletion_ShiftTab_CyclesBackwards() {
    var commandService = new CommandService();
    commandService.RegisterCommand("play", (_, __) => { });
    commandService.RegisterCommand("pause", (_, __) => { });
    commandService.RegisterCommand("playlist", (_, __) => { });

    using var context = NewContext();
    var commandLine = new CommandLine(commandService);
    context.Add(commandLine);

    context.KeyDown(Key.P);
    // Shift+Tab from start picks last item
    context.KeyDown(Key.Tab.WithShift);
    Assert.Equal(":playlist", commandLine.Text);

    // Shift+Tab again picks previous item
    context.KeyDown(Key.Tab.WithShift);
    Assert.Equal(":play", commandLine.Text);
  }

  [Fact]
  public void TabCompletion_ContainsSlash_DoesNotComplete() {
    var commandService = new CommandService();
    commandService.RegisterCommand("play", (_, __) => { });

    using var context = NewContext();
    var commandLine = new CommandLine(commandService);
    context.Add(commandLine);

    context.KeyDown(Key.P).KeyDown(new Key('/')).KeyDown(Key.A);
    Assert.Equal(":p/a", commandLine.Text);

    context.KeyDown(Key.Tab);
    Assert.Equal(":p/a", commandLine.Text);
  }

  [Fact]
  public void StartInput_FilterMode_RaisesEventsAndAccepts() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    context.Add(commandLine);

    var filterChanges = new List<string>();
    string? acceptedFilter = null;
    commandLine.FilterChanged += (_, text) => filterChanges.Add(text);
    commandLine.FilterAccepted += (_, text) => acceptedFilter = text;

    commandLine.StartInput(CommandLineMode.Filter);
    Assert.Equal(CommandLineMode.Filter, commandLine.InputMode);
    Assert.Equal("/", commandLine.Text);

    context.KeyDown(Key.R).KeyDown(Key.O).KeyDown(Key.C).KeyDown(Key.K);
    Assert.Equal("/rock", commandLine.Text);
    Assert.Equal(["r", "ro", "roc", "rock"], filterChanges);

    context.KeyDown(Key.Enter);
    Assert.Equal("rock", acceptedFilter);
  }

  [Fact]
  public void StartInput_FilterMode_EscapeRaisesCommandCancelled() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    context.Add(commandLine);

    bool cancelled = false;
    commandLine.CommandCancelled += (_, __) => cancelled = true;

    commandLine.StartInput(CommandLineMode.Filter);
    context.KeyDown(Key.Esc);
    Assert.True(cancelled);
  }

  [Fact]
  public void StartInput_FilterMode_LosesFocusResetsInputMode() {
    using var context = NewContext();
    var commandLine = NewCommandLine();
    var button = new Button();
    context.Add(commandLine).Add(button);

    commandLine.StartInput(CommandLineMode.Filter);
    Assert.Equal(CommandLineMode.Filter, commandLine.InputMode);

    context.Then((_) => button.SetFocus());
    Assert.Equal(CommandLineMode.Command, commandLine.InputMode);
  }
}