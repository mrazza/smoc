using System;
using System.Collections.Generic;
using System.Linq;

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

  private readonly Dictionary<string, CommandHandler> _commands = new();
  private readonly Dictionary<string, CompletionHandler> _completers = new();

  /// <summary>
  /// Delegate for handling command completion.
  /// </summary>
  /// <param name="command">The command name.</param>
  /// <param name="args">The arguments part of the command string.</param>
  /// <returns>A list of possible completions.</returns>
  public delegate IEnumerable<string> CompletionHandler(string command, string args);

  /// <summary>
  /// Registers a new command handler.
  /// </summary>
  /// <param name="command">The command name (e.g. "a" in the "a/<artist_name>" command).</param>
  /// <param name="handler">The handler to callback when the command is executed.</param>
  /// <exception cref="ArgumentException">Thrown if the command is already registered.</exception>
  public void RegisterCommand(string command, CommandHandler handler) {
    if (_commands.ContainsKey(command)) {
      throw new ArgumentException("Command already registered");
    }

    _commands.Add(command, handler);
  }

  /// <summary>
  /// Unregisters a command handler.
  /// </summary>
  /// <param name="command">The command name to unregister.</param>
  /// <exception cref="ArgumentException">Thrown if the command is not registered.</exception>
  public void UnregisterCommand(string command) {
    if (!_commands.Remove(command)) {
      throw new ArgumentException("Command not registered");
    }
  }

  /// <summary>
  /// Gets all registered command names sorted alphabetically.
  /// </summary>
  /// <returns>A collection of registered command names.</returns>
  public IReadOnlyCollection<string> GetRegisteredCommands() {
    return _commands.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
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

    if (_commands.TryGetValue(commandName, out var handler)) {
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
    _completers[command] = handler;
  }

  /// <summary>
  /// Unregisters a completion handler.
  /// </summary>
  /// <param name="command">The command name.</param>
  public void UnregisterCompleter(string command) {
    _completers.Remove(command);
  }

  /// <summary>
  /// Gets completions for a given command line or prefix.
  /// </summary>
  /// <param name="command">The full command line string or command prefix.</param>
  /// <returns>A list of possible completions.</returns>
  public IReadOnlyList<string> GetCompletions(string? command) {
    command ??= string.Empty;
    var argCutoff = command.IndexOf('/');
    var commandName = command;
    if (argCutoff > 0) {
      commandName = command[..argCutoff];
    }
    var args = argCutoff > 0 ? command[(argCutoff + 1)..] : string.Empty;

    if (argCutoff >= 0 && _completers.TryGetValue(commandName, out var handler)) {
      return handler(commandName, args).ToList();
    }

    if (argCutoff <= 0) {
      return _commands.Keys
          .Where(c => c.StartsWith(commandName, StringComparison.OrdinalIgnoreCase))
          .OrderBy(k => k, StringComparer.Ordinal)
          .ToList();
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