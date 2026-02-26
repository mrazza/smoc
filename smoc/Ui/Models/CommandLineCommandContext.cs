using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Smoc.Ui.Models;

/// <summary>
/// A binding for a command that is invoked by the command line.
/// </summary>
public record struct CommandLineCommandContext : ICommandContext {

  /// <summary>
  /// Creates a new instance of the <see cref="CommandLineCommandContext"/> class.
  /// </summary>
  /// <param name="command">The command that was invoked.</param>
  /// <param name="source">The source of the command.</param>
  /// <param name="binding">The binding that was invoked.</param>
  /// <param name="routing">The routing of the command.</param>
  /// <param name="commandText">The text that triggered the command.</param>
  public CommandLineCommandContext(Command command, WeakReference<View>? source, ICommandBinding? binding, CommandRouting routing, string commandText) {
    Command = command;
    Source = source;
    Binding = binding;
    CommandText = commandText;
    Routing = routing;
  }

  /// <inheritdoc />
  public Command Command { get; set; }

  /// <inheritdoc />
  public WeakReference<View>? Source { get; set; }

  /// <inheritdoc />
  public ICommandBinding? Binding { get; set; }

  /// <inheritdoc />
  public CommandRouting Routing { get; set; }

  /// <summary>
  /// The text that triggered the event.
  /// </summary>
  public string CommandText { get; set; }
}