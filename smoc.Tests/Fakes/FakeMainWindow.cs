using Smoc.Ui;
using Smoc.Ui.Components;
using Smoc.Ui.Models;

namespace smoc.Tests.Fakes;

public class FakeMainWindow : IMainWindow {
  public Mode CurrentMode { get; set; }
  public void SetMode(Mode mode) { CurrentMode = mode; }
  public void DisplayError(string message) { }
}
