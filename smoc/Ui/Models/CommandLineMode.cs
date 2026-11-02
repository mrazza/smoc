namespace Smoc.Ui.Models;

/// <summary>
/// Specifies the input mode for the command line.
/// </summary>
public enum CommandLineMode {
  /// <summary>
  /// Command input mode prefixed with ':'.
  /// </summary>
  Command,

  /// <summary>
  /// Interactive filter mode prefixed with '/'.
  /// </summary>
  Filter
}
