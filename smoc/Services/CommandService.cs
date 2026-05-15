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
  private readonly Dictionary<string, CompletionHandler> completers = new();

  /// <summary>
  /// Delegate for handling command completion.
  /// </summary>
  /// <param name="command">The command name.</param>
  /// <param name="args">The arguments part of the command string.</param>
  public delegate IEnumerable<string> CompletionHandler(string command, string args);

  /// <summary>
  /// Registers a new command handler.
  /// </summary>
  /// <param name="command">The command name (e.g. "a" in the "a/<artist_name>" command).</param>
  /// <param name="handler">The handler to callback when the command is executed.</param>
  /// <exception cref="ArgumentException">Thrown if the command is already registered.</exception>
  public void RegisterCommand(string command, CommandHandler handler) {
    if (commands.ContainsKey(command)) throw new ArgumentException("Command already registered");

    commands.Add(command, handler);
  }

  /// <summary>
  /// Unregisters a command handler.
  /// </summary>
  /// <param name="command">The command name to unregister.</param>
  public void UnregisterCommand(string command) {
    if (!commands.Remove(command)) throw new ArgumentException("Command not registered");
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
  /// Registers a new completion handler.
  /// </summary>
  /// <param name="command">The command name.</param>
  /// <param name="handler">The handler to callback when completions are requested.</param>
  public void RegisterCompleter(string command, CompletionHandler handler) {
    completers[command] = handler;
  }

  /// <summary>
  /// Gets completions for a given command line.
  /// </summary>
  /// <param name="command">The full command line string.</param>
  /// <returns>A list of possible completions.</returns>
  public IEnumerable<string> GetCompletions(string command) {
    var argCutoff = command.IndexOf('/');
    var commandName = command;
    if (argCutoff > 0) {
      commandName = command[..argCutoff];
    }
    var args = argCutoff > 0 ? command[(argCutoff + 1)..] : string.Empty;

    if (completers.TryGetValue(commandName, out var handler)) {
      return handler(commandName, args);
    }

    if (argCutoff <= 0) {
      return commands.Keys.Where(c => c.StartsWith(commandName, StringComparison.OrdinalIgnoreCase));
    }

    return [];
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
