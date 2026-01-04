namespace Smoc.Services;

public sealed class CommandService
{
    public delegate void CommandHandler(string command, string args);

    private readonly Dictionary<string, CommandHandler> commands = new();

    public void RegisterCommand(string command, CommandHandler handler)
    {
        if (commands.ContainsKey(command))
        {
            throw new ArgumentException("Command already registered");
        }

        commands.Add(command, handler);
    }

    public bool ExecuteCommand(string command)
    {
        var argCutoff = command.IndexOf('/');
        var commandName = command;
        if (argCutoff > 0)
        {
            commandName = command[..argCutoff];
        }
        var args = argCutoff > 0 ? command[argCutoff..] : string.Empty;

        if (commands.TryGetValue(commandName, out var handler))
        {
            handler(command, args);
            return true;
        }
        return false;
    }

    public static string[] GetArgs(string args)
    {
        var results = args.Split('/');

        if (results.Length > 0 && results[0].Trim() == string.Empty)
        {
            return results[1..];
        }

        return results;
    }
}