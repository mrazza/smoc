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
  public void GetCompletions_NoCompleter_ReturnsMatchingCommands() {
    var commandService = new CommandService();
    commandService.RegisterCommand("apple", (cmd, __) => { });
    commandService.RegisterCommand("apply", (cmd, __) => { });
    commandService.RegisterCommand("banana", (cmd, __) => { });

    var completions = commandService.GetCompletions("app");
    Assert.Equal(["apple", "apply"], completions.OrderBy(c => c));
  }

  [Fact]
  public void GetCompletions_WithCompleter_ReturnsMatchingArgs() {
    var commandService = new CommandService();
    commandService.RegisterCommand("fruit", (cmd, __) => { });
    commandService.RegisterCompleter("fruit", (cmd, args) => {
        string[] fruits = ["apple", "banana", "cherry"];
        return fruits.Where(f => f.StartsWith(args));
    });

    var completions = commandService.GetCompletions("fruit/a");
    Assert.Equal(["apple"], completions);
  }

  [Fact]
  public void GetCompletions_UnknownCommandWithArgs_ReturnsEmpty() {
    var commandService = new CommandService();
    var completions = commandService.GetCompletions("unknown/args");
    Assert.Empty(completions);
  }
}