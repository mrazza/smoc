using Terminal.Gui.App;
using Smoc.Ui.Components;
using Smoc.Services;
using Smoc.Ui.Models;
using Terminal.Gui.Configuration;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using System;
using System.Linq;
using Smoc.Services.Util;

namespace Smoc.Ui;

/// <summary>
/// The view for the command line at the bottom of the screen.
/// </summary>
public sealed class CommandLine : View {
  private readonly CommandTextField _commandTextField;
  private readonly Label _errorLabel;
  private readonly CommandService? _commandService;
  private object? _errorTimeoutTracker;

  /// <summary>
  /// Occurs when the command input is cancelled (e.g. via Esc).
  /// </summary>
  public event EventHandler? CommandCancelled;

  /// <summary>
  /// Initializes a new instance of the <see cref="CommandLine"/> class.
  /// </summary>
  /// <param name="commandService">The command service to use for completions.</param>
  public CommandLine(CommandService? commandService = null) {
    _commandService = commandService;
    Width = Dim.Fill();
    Height = Dim.Absolute(1);
    CanFocus = true;
    TabStop = TabBehavior.NoStop;
    SetScheme(SchemeManager.GetScheme("CommandLine"));

    _commandTextField = new CommandTextField() {
      Width = Dim.Fill()
    };
    _errorLabel = new Label() {
      X = Pos.Absolute(0),
      Y = Pos.Absolute(0),
      CanFocus = false
    };
    _errorLabel.SetScheme(SchemeManager.GetScheme("CommandLineError"));
    _commandTextField.TextChanging += OnTextChanging;
    Add(_commandTextField, _errorLabel);

    AddCommand(Command.Cancel, OnCancelCommand);
    AddCommand(Command.Accept, OnCommandAccepted);
    KeyBindings.Add(Key.Esc, Command.Cancel);
  }

  /// <summary>
  /// Displays a temporary error message in the command line area.
  /// </summary>
  /// <param name="message">The message to display.</param>
  public void DisplayError(string message) {
    Logging.Warning($"Displaying error: {message}");
    ClearError();
    _errorLabel.Text = message;
    _errorTimeoutTracker = App!.AddTimeout(TimeSpan.FromSeconds(5), () => { ClearError(); return false; });
  }

  /// <inheritdoc/>
  protected override bool OnKeyDownNotHandled(Key key) {
    if (key == Key.Tab && _commandService != null) {
      var text = _commandTextField.Text.TrimStart(':');
      var completions = _commandService.GetCompletions(text).ToList();
      if (completions.Count == 1) {
        var completion = completions[0];
        var argCutoff = text.IndexOf('/');
        if (argCutoff > 0) {
          _commandTextField.Text = $":{text[..(argCutoff + 1)]}{completion}";
        } else {
          _commandTextField.Text = $":{completion}/";
        }
        _commandTextField.InsertionPoint = _commandTextField.Text.Length;
      } else if (completions.Count > 1) {
        DisplayError($"Completions: {string.Join(", ", completions)}");
      }
      return true;
    }

    return base.OnKeyDownNotHandled(key);
  }

  /// <inheritdoc/>
  protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView) {
    base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
    if (newHasFocus) {
      ClearError();
      _commandTextField.InsertText(":");
    } else {
      _commandTextField.DeleteAll();
      _commandTextField.ClearHistoryChanges();
    }
  }

  private void ClearError() {
    if (_errorTimeoutTracker is not null) {
      App!.RemoveTimeout(_errorTimeoutTracker);
    }

    _errorLabel.Text = "";
    _errorTimeoutTracker = null;
  }

  private void OnTextChanging(object? sender, ResultEventArgs<string> e) {
    if (e.Result == string.Empty) {
      OnCancelCommand(null);
    }
  }

  private bool? OnCancelCommand(ICommandContext? context) {
    CommandCancelled?.Invoke(this, EventArgs.Empty);
    return true;
  }

  private bool? OnCommandAccepted(ICommandContext? ctx) {
    RaiseAccepted(new CommandLineCommandContext(Command.Accept, new WeakReference<View>(this), ctx?.Binding, ctx?.Routing ?? CommandRouting.Direct, _commandTextField.Text.TrimStart(':')));
    return true;
  }
}