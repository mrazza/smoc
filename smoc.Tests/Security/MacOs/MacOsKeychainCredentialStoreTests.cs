using Moq;
using Smoc.Security;
using Smoc.Security.MacOs;
using Xunit;

namespace smoc.Tests.Security.MacOs;

public class MacOsKeychainCredentialStoreTests {
  [Fact]
  public async Task MacOsKeychain_GetCredentialAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<IMacOsKeychainAdapter>();
    mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
    mockAdapter.Setup(a => a.GetSecretAsync("smoc:soundcloud", "token", It.IsAny<CancellationToken>()))
               .ReturnsAsync("mac_sc_token");

    var store = new MacOsKeychainCredentialStore(mockAdapter.Object);
    string? result = await store.GetCredentialAsync("smoc:soundcloud", "token", TestContext.Current.CancellationToken);

    Assert.Equal("mac_sc_token", result);
    Assert.True(await store.IsAvailableAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task MacOsKeychain_SetAndDelete_DelegatesToAdapter() {
    var mockAdapter = new Mock<IMacOsKeychainAdapter>();
    mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);

    var store = new MacOsKeychainCredentialStore(mockAdapter.Object);
    await store.SetCredentialAsync("smoc:subsonic", "user", "pass", TestContext.Current.CancellationToken);
    await store.DeleteCredentialAsync("smoc:subsonic", "user", TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.SetSecretAsync("smoc:subsonic", "user", "pass", It.IsAny<CancellationToken>()), Times.Once);
    mockAdapter.Verify(a => a.DeleteSecretAsync("smoc:subsonic", "user", It.IsAny<CancellationToken>()), Times.Once);
  }
}
