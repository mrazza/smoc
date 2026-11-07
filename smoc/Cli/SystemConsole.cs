using System.Text;

namespace Smoc.Cli;

/// <summary>
/// Default implementation of <see cref="IConsoleIO"/> backed by standard <see cref="Console"/>.
/// </summary>
public class SystemConsole : IConsoleIO {
  /// <inheritdoc />
  public bool IsInputRedirected => Console.IsInputRedirected;

  /// <inheritdoc />
  public string? ReadLine() => Console.ReadLine();

  /// <inheritdoc />
  public string ReadPassword(string prompt = "Password: ") {
    Write(prompt);

    if (IsInputRedirected) {
      string? redirectedInput = Console.ReadLine();
      return redirectedInput ?? string.Empty;
    }

    StringBuilder passwordBuilder = new();
    while (true) {
      ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

      if (keyInfo.Key == ConsoleKey.Enter) {
        WriteLine();
        break;
      }

      if (keyInfo.Key == ConsoleKey.Backspace) {
        if (passwordBuilder.Length > 0) {
          passwordBuilder.Length--;
          Write("\b \b");
        }
        continue;
      }

      if (keyInfo.Key == ConsoleKey.Escape) {
        while (passwordBuilder.Length > 0) {
          passwordBuilder.Length--;
          Write("\b \b");
        }
        continue;
      }

      if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && keyInfo.Key == ConsoleKey.C) {
        WriteLine();
        throw new OperationCanceledException("Password input was canceled.");
      }

      if (!char.IsControl(keyInfo.KeyChar)) {
        passwordBuilder.Append(keyInfo.KeyChar);
        Write("*");
      }
    }

    return passwordBuilder.ToString();
  }

  /// <inheritdoc />
  public bool Confirm(string prompt, bool defaultValue = true) {
    string choiceHint = defaultValue ? "[Y/n]" : "[y/N]";
    Write($"{prompt} {choiceHint} ");

    string? response = ReadLine()?.Trim();
    if (string.IsNullOrEmpty(response)) {
      return defaultValue;
    }

    return response.StartsWith("y", StringComparison.OrdinalIgnoreCase);
  }

  /// <inheritdoc />
  public void Write(string value) => Console.Write(value);

  /// <inheritdoc />
  public void WriteLine(string value) => Console.WriteLine(value);

  /// <inheritdoc />
  public void WriteLine() => Console.WriteLine();
}
