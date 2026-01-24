using Moq;
using Terminal.Gui.App;

namespace smoc.Tests.Fakes;

public static class FakeApplication {

  public static IApplication New() {
    var mock = new Mock<IApplication>();
    mock.Setup((a) => a.Invoke(It.IsAny<Action>())).Callback<Action>((action) => action());
    return mock.Object;
  }

}