using System.Text;
using Smoc.Cli;

namespace smoc.Tests.TestInfra;

/// <summary>
/// In-memory implementation of <see cref="IConsoleIO"/> for unit testing.
/// </summary>
public class TestConsoleIO : IConsoleIO {
  private readonly Queue<string> _inputs = new();
  private readonly Queue<bool> _confirmations = new();
  private readonly StringBuilder _output = new();

  public bool IsInputRedirected { get; set; } = false;

  public string Output => _output.ToString();

  public void QueueInput(string input) => _inputs.Enqueue(input);

  public void QueueConfirmation(bool confirmed) => _confirmations.Enqueue(confirmed);

  public string? ReadLine() {
    return _inputs.Count > 0 ? _inputs.Dequeue() : null;
  }

  public string ReadPassword(string prompt = "Password: ") {
    _output.Append(prompt);
    return _inputs.Count > 0 ? _inputs.Dequeue() : string.Empty;
  }

  public bool Confirm(string prompt, bool defaultValue = true) {
    _output.Append(prompt);
    return _confirmations.Count > 0 ? _confirmations.Dequeue() : defaultValue;
  }

  public void Write(string value) => _output.Append(value);

  public void WriteLine(string value) => _output.AppendLine(value);

  public void WriteLine() => _output.AppendLine();
}
