namespace Smoc.Cli;

/// <summary>
/// Abstraction for console input and output operations, enabling testability of CLI commands.
/// </summary>
public interface IConsoleIO {
  /// <summary>
  /// Gets whether input is redirected from a stream or pipe rather than an interactive console.
  /// </summary>
  bool IsInputRedirected { get; }

  /// <summary>
  /// Reads the next line of characters from the standard input stream.
  /// </summary>
  /// <returns>The next line of characters, or <c>null</c> if no more lines are available.</returns>
  string? ReadLine();

  /// <summary>
  /// Prompts the user and reads a line of input with characters masked as asterisks.
  /// </summary>
  /// <param name="prompt">The prompt string to display before reading input.</param>
  /// <returns>The entered password string.</returns>
  string ReadPassword(string prompt = "Password: ");

  /// <summary>
  /// Prompts the user with a question and reads a yes/no confirmation.
  /// </summary>
  /// <param name="prompt">The prompt message to display.</param>
  /// <param name="defaultValue">The default value if the user presses Enter without typing.</param>
  /// <returns><c>true</c> if confirmed; otherwise <c>false</c>.</returns>
  bool Confirm(string prompt, bool defaultValue = true);

  /// <summary>
  /// Writes the specified string value to the console.
  /// </summary>
  /// <param name="value">The string value to write.</param>
  void Write(string value);

  /// <summary>
  /// Writes the specified string value followed by the current line terminator to the console.
  /// </summary>
  /// <param name="value">The string value to write.</param>
  void WriteLine(string value);

  /// <summary>
  /// Writes the current line terminator to the console.
  /// </summary>
  void WriteLine();
}
