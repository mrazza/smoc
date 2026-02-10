using Moq;
using Terminal.Gui.App;

namespace smoc.Tests.Fakes;

/// <summary>
/// A fake implementation of <see cref="IApplication"/> for use in tests.
/// </summary>
public static class FakeApplication {

  /// <summary>
  /// Creates a new instance of <see cref="IApplication"/> for use in tests.
  /// </summary>
  /// <remarks>
  /// This implementation is a simple mock that allows for the execution of actions via Invoke.
  /// </remarks>
  /// <returns>A new instance of <see cref="IApplication"/> for use in tests.</returns>
  public static IApplication New() {
    var mock = new Mock<IApplication>();
    mock.Setup((a) => a.Invoke(It.IsAny<Action>())).Callback<Action>((action) => action());
    return mock.Object;
  }

}