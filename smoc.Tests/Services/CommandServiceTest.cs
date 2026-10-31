using Smoc.Services;

namespace smoc.Tests.Services;

public class CommandServiceTest {

  [Fact]
  public void ExecuteCommand_UnknownCommand_ReturnsFalse() {
    var commandService = new CommandService();
    Assert.False(commandService.ExecuteCommand("unknown"));
  }

  [Fact]
  public void ExecuteCommand_KnownCommand_ReturnsTrue() {
    var commandService = new CommandService();
    commandService.RegisterCommand("known", (_, __) => { });
    Assert.True(commandService.ExecuteCommand("known"));
  }

  [Fact]
  public void ExecuteCommand_KnownCommand_CallsHandler() {
    var commandService = new CommandService();
    bool called = false;
    commandService.RegisterCommand("known", (cmd, __) => {
      Assert.False(called);
      Assert.Equal("known", cmd);
      called = true;
    });
    commandService.ExecuteCommand("known");
    Assert.True(called);
  }

  [Fact]
  public void ExecuteCommand_KnownCommand_CallsHandlerWithEmptyArgs() {
    var commandService = new CommandService();
    bool called = false;
    commandService.RegisterCommand("known", (cmd, args) => {
      Assert.False(called);
      Assert.Equal("known", cmd);
      Assert.Equal(string.Empty, args);
      called = true;
    });
    commandService.ExecuteCommand("known");
    Assert.True(called);
  }

  [Fact]
  public void ExecuteCommand_KnownCommandWithTrailingSlash_CallsHandlerWithEmptyArgs() {
    var commandService = new CommandService();
    bool called = false;
    commandService.RegisterCommand("known", (cmd, args) => {
      Assert.False(called);
      Assert.Equal("known", cmd);
      Assert.Equal(string.Empty, args);
      called = true;
    });
    commandService.ExecuteCommand("known/");
    Assert.True(called);
  }

  [Fact]
  public void ExecuteCommand_KnownCommandWithArgs_CallsHandlerWithCorrectArguments() {
    var commandService = new CommandService();
    bool called = false;
    commandService.RegisterCommand("known", (cmd, args) => {
      Assert.False(called);
      Assert.Equal("known", cmd);
      Assert.Equal("correct/args", args);
      called = true;
    });
    commandService.ExecuteCommand("known/correct/args");
    Assert.True(called);
  }

  [Fact]
  public void GetArgs_NoArgs_ReturnsEmptyString() {
    Assert.Equal([], CommandService.GetArgs(string.Empty));
  }

  [Fact]
  public void GetArgs_WithArgs_ReturnsArgs() {
    Assert.Equal(["correct", "args"], CommandService.GetArgs("correct/args"));
  }

  [Fact]
  public void GetArgs_WithArgsAndTrailingSlash_ReturnsArgs() {
    Assert.Equal(["correct", "args"], CommandService.GetArgs("correct/args/"));
  }

  [Fact]
  public void GetArgs_EmptyArgs_NoEmpty() {
    Assert.Equal(["correct", "args"], CommandService.GetArgs("correct//args"));
  }

  [Fact]
  public void RegisterCommand_DuplicateCommand_ThrowsException() {
    var commandService = new CommandService();
    commandService.RegisterCommand("known", (cmd, __) => { });
    Assert.Throws<ArgumentException>(() => commandService.RegisterCommand("known", (cmd, __) => { }));
  }

  [Fact]
  public void GetRegisteredCommands_Empty_ReturnsEmpty() {
    var commandService = new CommandService();
    Assert.Empty(commandService.GetRegisteredCommands());
  }

  [Fact]
  public void GetRegisteredCommands_MultipleCommands_ReturnsSorted() {
    var commandService = new CommandService();
    commandService.RegisterCommand("z_command", (_, __) => { });
    commandService.RegisterCommand("a_command", (_, __) => { });
    commandService.RegisterCommand("m_command", (_, __) => { });
    Assert.Equal(["a_command", "m_command", "z_command"], commandService.GetRegisteredCommands());
  }

  [Fact]
  public void GetCompletions_EmptyPrefix_ReturnsAllSorted() {
    var commandService = new CommandService();
    commandService.RegisterCommand("play", (_, __) => { });
    commandService.RegisterCommand("pause", (_, __) => { });
    commandService.RegisterCommand("stop", (_, __) => { });
    Assert.Equal(["pause", "play", "stop"], commandService.GetCompletions(string.Empty));
  }

  [Fact]
  public void GetCompletions_NullPrefix_ReturnsAllSorted() {
    var commandService = new CommandService();
    commandService.RegisterCommand("play", (_, __) => { });
    commandService.RegisterCommand("pause", (_, __) => { });
    Assert.Equal(["pause", "play"], commandService.GetCompletions(null!));
  }

  [Fact]
  public void GetCompletions_MatchingPrefix_ReturnsMatchingSorted() {
    var commandService = new CommandService();
    commandService.RegisterCommand("play", (_, __) => { });
    commandService.RegisterCommand("pause", (_, __) => { });
    commandService.RegisterCommand("stop", (_, __) => { });
    commandService.RegisterCommand("prev", (_, __) => { });
    Assert.Equal(["pause", "play", "prev"], commandService.GetCompletions("p"));
  }

  [Fact]
  public void GetCompletions_CaseInsensitiveMatch_ReturnsMatching() {
    var commandService = new CommandService();
    commandService.RegisterCommand("Play", (_, __) => { });
    commandService.RegisterCommand("pause", (_, __) => { });
    Assert.Equal(["Play", "pause"], commandService.GetCompletions("P"));
    Assert.Equal(["Play", "pause"], commandService.GetCompletions("p"));
  }

  [Fact]
  public void GetCompletions_NoMatch_ReturnsEmpty() {
    var commandService = new CommandService();
    commandService.RegisterCommand("play", (_, __) => { });
    commandService.RegisterCommand("pause", (_, __) => { });
    Assert.Empty(commandService.GetCompletions("xyz"));
  }
}