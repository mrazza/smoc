using Moq;
using Smoc.Security;
using Xunit;

namespace smoc.Tests.Security;

public class LinuxSecretServiceCredentialStoreTests {
  [Fact]
  public async Task LinuxSecretService_GetCredentialAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<ISecretServiceAdapter>();
    mockAdapter.Setup(a => a.GetSecretAsync("smoc:subsonic", "razza", It.IsAny<CancellationToken>()))
               .ReturnsAsync("subsonic_pass");

    var store = new LinuxSecretServiceCredentialStore(mockAdapter.Object);
    string? result = await store.GetCredentialAsync("smoc:subsonic", "razza", TestContext.Current.CancellationToken);

    Assert.Equal("subsonic_pass", result);
  }

  [Fact]
  public async Task LinuxSecretService_SetCredentialAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<ISecretServiceAdapter>();
    var store = new LinuxSecretServiceCredentialStore(mockAdapter.Object);

    await store.SetCredentialAsync("smoc:subsonic", "razza", "my_pass", TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.SetSecretAsync("smoc:subsonic", "razza", "my_pass", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task LinuxSecretService_DeleteCredentialAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<ISecretServiceAdapter>();
    var store = new LinuxSecretServiceCredentialStore(mockAdapter.Object);

    await store.DeleteCredentialAsync("smoc:subsonic", "razza", TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.DeleteSecretAsync("smoc:subsonic", "razza", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task LinuxSecretService_IsAvailableAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<ISecretServiceAdapter>();
    mockAdapter.Setup(a => a.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    var store = new LinuxSecretServiceCredentialStore(mockAdapter.Object);
    bool available = await store.IsAvailableAsync(TestContext.Current.CancellationToken);

    Assert.True(available);
  }
}
