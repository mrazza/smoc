using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Smoc.Ui.Models;

/// <summary>
/// A binding for a command that is invoked by the command line.
/// </summary>
/// <param name="commands">The Terminal.Gui command(s) that are bound to the event.</param>
/// <param name="source">The source of the command.</param>
/// <param name="commandText">The text that triggered the command.</param>
public record struct CommandLineCommandContext : ICommandContext {

  /// <summary>
  /// Creates a new instance of the <see cref="CommandLineCommandContext"/> class.
  /// </summary>
  /// <param name="command">The command that was invoked.</param>
  /// <param name="source">The source of the command.</param>
  /// <param name="binding">The binding that was invoked.</param>
  /// <param name="commandText">The text that triggered the command.</param>
  public CommandLineCommandContext(Command command, View? source, IInputBinding? binding, string commandText) {
    Command = command;
    Source = source;
    Binding = binding;
    CommandText = commandText;
  }

  /// <inheritdoc />
  public Command Command { get; set; }

  /// <inheritdoc />
  public View? Source { get; set; }

  /// <inheritdoc />
  public IInputBinding? Binding { get; set; }

  /// <summary>
  /// The text that triggered the event.
  /// </summary>
  public string CommandText { get; set; }
}