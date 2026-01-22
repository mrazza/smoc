namespace Smoc.Services;

/// <summary>
/// Service for registering and executing text-based commands.
/// </summary>
public sealed class CommandService {
  /// <summary>
  /// Delegate for handling command execution.
  /// </summary>
  /// <param name="command">The full command string used to invoke the handler.</param>
  /// <param name="args">The arguments part of the command string.</param>
  public delegate void CommandHandler(string command, string args);

  private readonly Dictionary<string, CommandHandler> commands = new();

  /// <summary>
  /// Registers a new command handler.
  /// </summary>
  /// <param name="command">The command name (e.g. "a" in the "a/<artist_name>" command).</param>
  /// <param name="handler">The handler to callback when the command is executed.</param>
  /// <exception cref="ArgumentException">Thrown if the command is already registered.</exception>
  public void RegisterCommand(string command, CommandHandler handler) {
    if (commands.ContainsKey(command)) {
      throw new ArgumentException("Command already registered");
    }

    commands.Add(command, handler);
  }

  /// <summary>
  /// Executes a command if a matching handler is found.
  /// </summary>
  /// <param name="command">The command string to execute.</param>
  /// <returns><c>true</c> if the command was found and executed; otherwise, <c>false</c>.</returns>
  public bool ExecuteCommand(string command) {
    var argCutoff = command.IndexOf('/');
    var commandName = command;
    if (argCutoff > 0) {
      commandName = command[..argCutoff];
    }
    var args = argCutoff > 0 ? command[(argCutoff + 1)..] : string.Empty;

    if (commands.TryGetValue(commandName, out var handler)) {
      handler(commandName, args);
      return true;
    }
    return false;
  }

  /// <summary>
  /// Parses arguments from a command string.
  /// </summary>
  /// <param name="args">The raw arguments string.</param>
  /// <returns>An array of individual arguments.</returns>
  public static string[] GetArgs(string args) {
    return args.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
  }
}
