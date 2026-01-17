using System.Reflection;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Models;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui;

public sealed class MainWindow : Runnable
{
    private readonly CommandLine commandLine;
    private readonly StatusBar statusBar;
    private readonly MainContent mainContent;
    private readonly NowPlaying nowPlaying;
    private readonly PlayerService playerService;
    private readonly CommandService commandService;
    private readonly IStreamingClient streamingClient;

    private Mode? currentMode;
    private View? preCommandFocusedView;
    private Mode? preCommandMode;

    public MainWindow(IStreamingClient streamingClient)
    {
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        this.streamingClient = streamingClient;
        playerService = new PlayerService(this, streamingClient);
        commandService = new CommandService();
        nowPlaying = new NowPlaying(this, playerService, commandService);
        commandLine = new CommandLine()
        {
            Y = Pos.AnchorEnd()
        };
        statusBar = new StatusBar(playerService)
        {
            Y = Pos.Top(commandLine) - 1
        };
        mainContent = new MainContent(this, commandService, playerService, streamingClient)
        {
            Y = Pos.Bottom(nowPlaying),
            Height = Dim.Fill() - statusBar.Height - commandLine.Height
        };
        Add(nowPlaying, mainContent, statusBar, commandLine);

        commandService.RegisterCommand("q", (_, args) =>
        {
            if (args.Length > 0)
            {
                commandLine.DisplayError($"unexpected trailing characters: {args}");
            }
            else
            {
                App!.RequestStop();
            }
        });

        AddCommand(Command.HotKey, OnCommandLineHotKey);
        HotKeyBindings.Add(new Key(':'), this, Command.HotKey);

        commandLine.CommandCancelled += (sender, e) =>
        {
            SetMode(preCommandMode!.Value);
            preCommandFocusedView?.SetFocus();
        };
        commandLine.Accepted += (sender, e) =>
        {
            var command = (e.Context as CommandContext<string>?)?.Binding;
            SetMode(preCommandMode!.Value);
            preCommandFocusedView?.SetFocus();

            if (command is not null && command.Length > 0)
            {
                if (!commandService.ExecuteCommand(command!))
                {
                    commandLine.DisplayError($"not a valid commmand: {command}");
                }
            }
        };

        currentMode = null;
        SetMode(Mode.Player);
        mainContent.SetFocus();
    }

    public void SetMode(Mode mode)
    {
        if (mode == currentMode)
        {
            return;
        }

        if (mode != Mode.Command)
        {
            mainContent.SetMode(mode);
        }
        else
        {
            commandLine.SetFocus();
        }

        currentMode = mode;
        statusBar.SetMode(GetModeDisplayName(mode));
    }

    public void DisplayError(string message)
    {
        commandLine.DisplayError(message);
    }

    private bool? OnCommandLineHotKey(ICommandContext? context)
    {
        preCommandMode = currentMode;
        preCommandFocusedView = MostFocused;
        SetMode(Mode.Command);
        return true;
    }

    private static string GetModeDisplayName(Mode mode)
    {
        FieldInfo? fieldInfo = typeof(Mode).GetField(mode.ToString());
        if (fieldInfo is not null)
        {
            object[] attributes = fieldInfo.GetCustomAttributes(typeof(DisplayNameAttribute), true);
            if (attributes.Length > 0)
            {
                return ((DisplayNameAttribute)attributes[0]).DisplayName;
            }
        }
        throw new ArgumentException("Invalid mode");
    }
}