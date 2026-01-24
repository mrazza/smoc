using Moq;
using Smoc.Services.Util;

namespace smoc.Tests.Services.Util;

public class UniqueResourceTest {

  private readonly Mock<IDisposable> _mockDisposable;

  public UniqueResourceTest() {
    _mockDisposable = new Mock<IDisposable>();
  }

  [Fact]
  public void NoResource_ReturnsNull() {
    var sut = new UniqueResource<IDisposable>();
    Assert.Null(sut.Resource);
  }

  [Fact]
  public void NoResource_Dispose_DoesNothing() {
    bool disposeActionCalled = false;
    var sut = new UniqueResource<IDisposable>((_) => disposeActionCalled = true);
    sut.Dispose();
    _mockDisposable.Verify(d => d.Dispose(), Times.Never());
    Assert.False(disposeActionCalled);
  }

  [Fact]
  public void NewWithResource_ReturnsResource() {
    var sut = new UniqueResource<IDisposable>(_mockDisposable.Object, (_) => { });
    Assert.Equal(_mockDisposable.Object, sut.Resource);
  }

  [Fact]
  public void ReplaceNullResource_ReturnsResource() {
    var sut = new UniqueResource<IDisposable>();
    sut.Replace(_mockDisposable.Object);
    Assert.Equal(_mockDisposable.Object, sut.Resource);
  }

  [Fact]
  public void HasResource_Dispose_DisposesResource() {
    var sut = new UniqueResource<IDisposable>(_mockDisposable.Object, (_) => { });
    sut.Dispose();
    _mockDisposable.Verify(d => d.Dispose(), Times.Once());
  }

  [Fact]
  public void HasResource_Dispose_CallsAction() {
    bool disposeActionCalled = false;
    IDisposable? actionResource = null;
    var sut = new UniqueResource<IDisposable>(_mockDisposable.Object, (d) => { actionResource = d; disposeActionCalled = true; });
    sut.Dispose();
    Assert.True(disposeActionCalled);
    Assert.Equal(_mockDisposable.Object, actionResource);
  }

  [Fact]
  public void ReplaceResource_ReturnsNewResource() {
    var newMockDisposable = new Mock<IDisposable>();
    var sut = new UniqueResource<IDisposable>(_mockDisposable.Object, (_) => { });
    sut.Replace(newMockDisposable.Object);
    Assert.Equal(newMockDisposable.Object, sut.Resource);
  }

  [Fact]
  public void ReplaceResource_CallsAction() {
    var newMockDisposable = new Mock<IDisposable>();
    bool disposeActionCalled = false;
    IDisposable? actionResource = null;
    var sut = new UniqueResource<IDisposable>(_mockDisposable.Object, (d) => { actionResource = d; disposeActionCalled = true; });
    sut.Replace(newMockDisposable.Object);
    Assert.True(disposeActionCalled);
    Assert.Equal(_mockDisposable.Object, actionResource);
  }

  [Fact]
  public void ReplaceResource_DisposesOldResource() {
    var newMockDisposable = new Mock<IDisposable>();
    var sut = new UniqueResource<IDisposable>(_mockDisposable.Object, (_) => { });
    sut.Replace(newMockDisposable.Object);
    _mockDisposable.Verify(d => d.Dispose(), Times.Once());
  }

}