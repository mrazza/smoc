using smoc.Tests.TestInfra;
using Smoc.Ui.Components;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using AppTestHelpers;

namespace smoc.Tests.Ui.Components;

public class CommandTextFieldTest {

  private ScreenshotDiffer _screenshotDiffer;

  public CommandTextFieldTest(ITestOutputHelper output) {
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private static CommandTextField NewCommandTextField() => new();

  private static AppTestHelper NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());

  [Fact]
  public void TabStop_WontFocus() {
    using var context = NewContext();
    var commandTextField = NewCommandTextField();
    context.Add(commandTextField).Add(new Button());
    Assert.False(commandTextField.HasFocus);
    context.KeyDown(Key.Tab);
    Assert.False(commandTextField.HasFocus);
  }

  [Fact]
  public void Focused_WillFocus() {
    using var context = NewContext();
    var commandTextField = NewCommandTextField();
    context.Add(commandTextField).Add(new Button());
    Assert.False(commandTextField.HasFocus);
    context.Then((_) => commandTextField.SetFocus());
    Assert.True(commandTextField.HasFocus);
  }

  [Fact]
  public void Focused_Tab_KeepsFocus() {
    using var context = NewContext();
    var commandTextField = NewCommandTextField();
    context.Add(commandTextField).Add(new Button()).Then((_) => commandTextField.SetFocus());
    Assert.True(commandTextField.HasFocus);
    context.KeyDown(Key.Tab);
    Assert.True(commandTextField.HasFocus);
  }

  [Fact]
  public void Focused_AdvanceFocus_KeepsFocus() {
    using var context = NewContext();
    var commandTextField = NewCommandTextField();
    context.Add(commandTextField).Add(new Button()).Then((_) => commandTextField.SetFocus());
    Assert.True(commandTextField.HasFocus);
    context.Then((_) => commandTextField.AdvanceFocus(NavigationDirection.Forward, null));
    Assert.True(commandTextField.HasFocus);
  }

  [Fact]
  public void Focused_ForceUnfocus_LosesFocus() {
    using var context = NewContext();
    var commandTextField = NewCommandTextField();
    var button = new Button();
    context.Add(commandTextField).Add(button).Then((_) => commandTextField.SetFocus());
    Assert.True(commandTextField.HasFocus);
    context.Then((_) => button.SetFocus());
    Assert.False(commandTextField.HasFocus);
  }

  [Fact]
  public void Focused_AcceptsText() {
    using var context = NewContext();
    var commandTextField = NewCommandTextField();
    context.Add(commandTextField)
        .KeyDown(Key.H)
        .KeyDown(Key.E)
        .KeyDown(Key.L)
        .KeyDown(Key.L)
        .KeyDown(Key.O);
    Assert.Equal("hello", commandTextField.Text);
  }
}
