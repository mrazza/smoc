using Smoc.Services;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui;

/// <summary>
/// The view for the command line at the bottom of the screen.
/// </summary>
public sealed class CommandLine : View {
  private readonly CommandTextField _commandTextField;
  private readonly Label _errorLabel;
  private readonly List<string> _history = [];
  private object? _errorTimeoutTracker;
  private int _historyIndex = -1;
  private string _currentDraft = string.Empty;
  private List<string>? _tabCompletions;
  private int _tabCompletionIndex = -1;
  private bool _suppressCancel;

  /// <summary>
  /// Gets the command history.
  /// </summary>
  public IReadOnlyList<string> History => _history;

  /// <summary>
  /// Gets the current text in the command line text field.
  /// </summary>
  public new string Text => _commandTextField.Text;

  /// <summary>
  /// Gets or sets the command service used for autocomplete and completions.
  /// </summary>
  public CommandService? CommandService { get; set; }

  /// <summary>
  /// Gets the current command line input mode.
  /// </summary>
  public CommandLineMode InputMode { get; private set; } = CommandLineMode.Command;

  private char PrefixChar => InputMode == CommandLineMode.Filter ? '/' : ':';

  /// <summary>
  /// Occurs when the filter text changes during '/' input mode.
  /// </summary>
  public event EventHandler<string>? FilterChanged;

  /// <summary>
  /// Occurs when the filter is accepted in '/' mode.
  /// </summary>
  public event EventHandler<string>? FilterAccepted;

  /// <summary>
  /// Occurs when the command input is cancelled (e.g. via Esc).
  /// </summary>
  public event EventHandler? CommandCancelled;

  /// <summary>
  /// Initializes a new instance of the <see cref="CommandLine"/> class.
  /// </summary>
  public CommandLine() : this(null) { }

  /// <summary>
  /// Initializes a new instance of the <see cref="CommandLine"/> class with an optional command service.
  /// </summary>
  /// <param name="commandService">The command service to use for completions.</param>
  public CommandLine(CommandService? commandService) {
    CommandService = commandService;
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
    _commandTextField.KeyDown += OnCommandTextFieldKeyDown;
    Add(_commandTextField, _errorLabel);

    AddCommand(Command.Cancel, OnCancelCommand);
    AddCommand(Command.Accept, OnCommandAccepted);
    KeyBindings.Add(Key.Esc, Command.Cancel);
  }

  /// <summary>
  /// Starts input with the specified command line mode (Command for ':', Filter for '/').
  /// </summary>
  /// <param name="mode">The command line mode to use.</param>
  public void StartInput(CommandLineMode mode = CommandLineMode.Command) {
    _suppressCancel = true;
    try {
      InputMode = mode;
      _commandTextField.DeleteAll();
      _commandTextField.ClearHistoryChanges();
      _historyIndex = -1;
      _currentDraft = string.Empty;
      ResetTabCompletion();
      ClearError();
      if (!HasFocus) {
        SetFocus();
      } else {
        _commandTextField.InsertText(PrefixChar.ToString());
      }
    } finally {
      _suppressCancel = false;
    }
  }

  /// <summary>
  /// Displays a temporary error message in the command line area.
  /// </summary>
  /// <param name="message">The message to display.</param>
  public void DisplayError(string message) {
    Logging.Warning($"Displaying error: {message}");
    ClearError();
    _errorLabel.Text = message;
    _errorTimeoutTracker = App?.AddTimeout(TimeSpan.FromSeconds(5), () => { ClearError(); return false; });
  }

  /// <inheritdoc />
  protected override bool OnKeyDown(Key key) {
    if (HandleSpecialKey(key)) {
      key.Handled = true;
      return true;
    }

    return base.OnKeyDown(key);
  }

  /// <inheritdoc />
  protected override bool OnKeyDownNotHandled(Key key) {
    if (HandleSpecialKey(key)) {
      key.Handled = true;
      return true;
    }

    if (key == Key.Tab) {
      return true;
    }

    return base.OnKeyDownNotHandled(key);
  }

  /// <inheritdoc />
  protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView) {
    base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
    if (newHasFocus) {
      ClearError();
      if (string.IsNullOrEmpty(_commandTextField.Text)) {
        _commandTextField.InsertText(PrefixChar.ToString());
      }
    } else {
      InputMode = CommandLineMode.Command;
      _historyIndex = -1;
      _currentDraft = string.Empty;
      ResetTabCompletion();
      _commandTextField.DeleteAll();
      _commandTextField.ClearHistoryChanges();
    }
  }

  private void OnCommandTextFieldKeyDown(object? sender, Key key) {
    if (HandleSpecialKey(key)) {
      key.Handled = true;
    }
  }

  private bool HandleSpecialKey(Key key) {
    if (key.Handled) {
      return true;
    }

    if (key == Key.CursorUp) {
      ResetTabCompletion();
      return HandleCursorUp();
    }

    if (key == Key.CursorDown) {
      ResetTabCompletion();
      return HandleCursorDown();
    }

    if (key == Key.Tab || key == Key.Tab.WithShift) {
      return HandleTab(key == Key.Tab.WithShift);
    }

    ResetTabCompletion();
    return false;
  }

  private bool HandleCursorUp() {
    if (InputMode != CommandLineMode.Command) {
      return true;
    }

    if (_history.Count == 0) {
      return true;
    }

    if (_historyIndex == -1) {
      _currentDraft = _commandTextField.Text.Length > 1 ? _commandTextField.Text[1..] : string.Empty;
      _historyIndex = _history.Count - 1;
    } else if (_historyIndex > 0) {
      _historyIndex--;
    }

    _commandTextField.Text = $"{PrefixChar}{_history[_historyIndex]}";
    _commandTextField.InsertionPoint = _commandTextField.Text.Length;
    return true;
  }

  private bool HandleCursorDown() {
    if (InputMode != CommandLineMode.Command) {
      return true;
    }

    if (_historyIndex == -1) {
      return true;
    }

    if (_historyIndex < _history.Count - 1) {
      _historyIndex++;
      _commandTextField.Text = $"{PrefixChar}{_history[_historyIndex]}";
    } else {
      _historyIndex = -1;
      _commandTextField.Text = $"{PrefixChar}{_currentDraft}";
    }

    _commandTextField.InsertionPoint = _commandTextField.Text.Length;
    return true;
  }

  private bool HandleTab(bool isShift) {
    if (InputMode != CommandLineMode.Command || CommandService == null) {
      return true;
    }

    if (_tabCompletions == null) {
      var input = _commandTextField.Text.TrimStart(':');
      if (input.Contains('/')) {
        return true;
      }

      _tabCompletions = CommandService.GetCompletions(input).ToList();
      if (_tabCompletions.Count == 0) {
        return true;
      }

      _tabCompletionIndex = isShift ? _tabCompletions.Count - 1 : 0;
    } else {
      if (isShift) {
        _tabCompletionIndex = (_tabCompletionIndex - 1 + _tabCompletions.Count) % _tabCompletions.Count;
      } else {
        _tabCompletionIndex = (_tabCompletionIndex + 1) % _tabCompletions.Count;
      }
    }

    _commandTextField.Text = $":{_tabCompletions[_tabCompletionIndex]}";
    _commandTextField.InsertionPoint = _commandTextField.Text.Length;
    return true;
  }

  private void ResetTabCompletion() {
    _tabCompletions = null;
    _tabCompletionIndex = -1;
  }

  private void ClearError() {
    if (_errorTimeoutTracker is not null) {
      App?.RemoveTimeout(_errorTimeoutTracker);
    }

    _errorLabel.Text = "";
    _errorTimeoutTracker = null;
  }

  private void OnTextChanging(object? sender, ResultEventArgs<string> e) {
    if (string.IsNullOrEmpty(e.Result)) {
      if (!_suppressCancel) {
        OnCancelCommand(null);
      }
      return;
    }

    if (!_suppressCancel && InputMode == CommandLineMode.Filter && e.Result.Length >= 1 && e.Result.StartsWith('/')) {
      FilterChanged?.Invoke(this, e.Result[1..]);
    }
  }

  private bool? OnCancelCommand(ICommandContext? context) {
    _historyIndex = -1;
    _currentDraft = string.Empty;
    ResetTabCompletion();
    CommandCancelled?.Invoke(this, EventArgs.Empty);
    return true;
  }

  private bool? OnCommandAccepted(ICommandContext? ctx) {
    if (InputMode == CommandLineMode.Command) {
      var commandText = _commandTextField.Text.TrimStart(':');
      if (!string.IsNullOrEmpty(commandText)) {
        if (_history.Count == 0 || _history[^1] != commandText) {
          _history.Add(commandText);
        }
      }
      _historyIndex = -1;
      _currentDraft = string.Empty;
      RaiseAccepted(new CommandLineCommandContext(Command.Accept, new WeakReference<View>(this), ctx?.Binding, ctx?.Routing ?? CommandRouting.Direct, commandText));
    } else if (InputMode == CommandLineMode.Filter) {
      var filterText = _commandTextField.Text.Length > 1 ? _commandTextField.Text[1..] : string.Empty;
      FilterAccepted?.Invoke(this, filterText);
      RaiseAccepted(new CommandLineCommandContext(Command.Accept, new WeakReference<View>(this), ctx?.Binding, ctx?.Routing ?? CommandRouting.Direct, $"/{filterText}"));
    }
    return true;
  }
}
