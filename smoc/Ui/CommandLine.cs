using Smoc.Ui.Components;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui;

public sealed class CommandLine : View {
  private readonly CommandTextField _commandTextField;
  private readonly Label _errorLabel;
  private object? _errorTimeoutTracker;

  public event EventHandler? CommandCancelled;

  public CommandLine() {
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

  public void DisplayError(string message) {
    Logging.Warning($"Displaying error: {message}");
    ClearError();
    _errorLabel.Text = message;
    _errorTimeoutTracker = App!.AddTimeout(TimeSpan.FromSeconds(5), () => { ClearError(); return false; });
  }

  protected override bool OnKeyDownNotHandled(Key key) {
    if (key == Key.Tab) {
      return true;
    }

    return base.OnKeyDownNotHandled(key);
  }

  protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView) {
    base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
    if (newHasFocus) {
      ClearError();
      _commandTextField.InsertText(":");
    }
    else {
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
    this.RaiseAccepted(new CommandContext<string>(Command.Accept, this, _commandTextField.Text.TrimStart(':')));
    return true;
  }
}
