using Smoc.Cli;
using Xunit;

namespace smoc.Tests.Cli;

public class SystemConsoleTests {
  [Fact]
  public void SystemConsole_IsInputRedirected_MatchesConsoleProperty() {
    SystemConsole console = new();
    Assert.Equal(Console.IsInputRedirected, console.IsInputRedirected);
  }

  [Fact]
  public void SystemConsole_Confirm_WithRedirectedInput_ParsesYesAndNo() {
    // When input is redirected via TextReader, Confirm reads from Console.In
    var originalIn = Console.In;
    try {
      using var stringReader = new StringReader("y\n");
      Console.SetIn(stringReader);

      SystemConsole console = new();
      bool result = console.Confirm("Test confirm?", defaultValue: false);
      Assert.True(result);
    } finally {
      Console.SetIn(originalIn);
    }
  }

  [Fact]
  public void SystemConsole_Confirm_WithEmptyInput_ReturnsDefaultValue() {
    var originalIn = Console.In;
    try {
      using var stringReader = new StringReader("\n");
      Console.SetIn(stringReader);

      SystemConsole console = new();
      bool result = console.Confirm("Test confirm?", defaultValue: true);
      Assert.True(result);

      using var stringReaderFalse = new StringReader("\n");
      Console.SetIn(stringReaderFalse);
      bool resultFalse = console.Confirm("Test confirm?", defaultValue: false);
      Assert.False(resultFalse);
    } finally {
      Console.SetIn(originalIn);
    }
  }

  [Fact]
  public void SystemConsole_ReadPassword_WhenRedirected_ReadsLine() {
    var originalIn = Console.In;
    try {
      using var stringReader = new StringReader("myPassword123\n");
      Console.SetIn(stringReader);

      SystemConsole console = new();
      string pass = console.ReadPassword("Password: ");
      Assert.Equal("myPassword123", pass);
    } finally {
      Console.SetIn(originalIn);
    }
  }
}
