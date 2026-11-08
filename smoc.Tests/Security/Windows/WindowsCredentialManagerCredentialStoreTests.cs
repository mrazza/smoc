using Moq;
using Smoc.Security.Windows;
using Xunit;

namespace smoc.Tests.Security.Windows;

public class WindowsCredentialManagerCredentialStoreTests {
  [Fact]
  public async Task WindowsStore_SmallCredential_WritesToCredentialManager() {
    var mockAdapter = new Mock<IWindowsCredentialAdapter>();
    mockAdapter.Setup(a => a.WriteCredential(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
               .Returns(true);

    var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
    string smallSecret = "standard_password";

    await store.SetCredentialAsync("smoc:subsonic", "razza", smallSecret, TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.WriteCredential("smoc:subsonic:razza", "razza", smallSecret), Times.Once);
    mockAdapter.Verify(a => a.DeleteDpapiSecret("smoc:subsonic:razza"), Times.Once);
    mockAdapter.Verify(a => a.WriteDpapiSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task WindowsStore_SmallCredential_FallsBackToDpapiWhenCredWriteFails() {
    var mockAdapter = new Mock<IWindowsCredentialAdapter>();
    mockAdapter.Setup(a => a.WriteCredential(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
               .Returns(false);

    var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
    string smallSecret = "standard_password";

    await store.SetCredentialAsync("smoc:subsonic", "razza", smallSecret, TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.WriteCredential("smoc:subsonic:razza", "razza", smallSecret), Times.Once);
    mockAdapter.Verify(a => a.WriteDpapiSecretAsync("smoc:subsonic:razza", smallSecret, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task WindowsStore_LargePayload_WritesToDpapiSecretAndDeletesCredentialManager() {
    var mockAdapter = new Mock<IWindowsCredentialAdapter>();

    var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
    string largeCookies = new('A', 3500);

    await store.SetCredentialAsync("smoc:youtubemusic", "cookies", largeCookies, TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.WriteDpapiSecretAsync("smoc:youtubemusic:cookies", largeCookies, It.IsAny<CancellationToken>()), Times.Once);
    mockAdapter.Verify(a => a.WriteCredential(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    mockAdapter.Verify(a => a.DeleteCredential("smoc:youtubemusic:cookies"), Times.Once);
  }

  [Fact]
  public async Task WindowsStore_GetCredential_PrefersDpapiWhenPresent() {
    var mockAdapter = new Mock<IWindowsCredentialAdapter>();
    mockAdapter.Setup(a => a.ReadDpapiSecretAsync("smoc:youtubemusic:cookies", It.IsAny<CancellationToken>()))
               .ReturnsAsync("large_cookie_val");

    var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
    string? retrieved = await store.GetCredentialAsync("smoc:youtubemusic", "cookies", TestContext.Current.CancellationToken);

    Assert.Equal("large_cookie_val", retrieved);
    mockAdapter.Verify(a => a.ReadDpapiSecretAsync("smoc:youtubemusic:cookies", It.IsAny<CancellationToken>()), Times.Once);
    string? dummy;
    mockAdapter.Verify(a => a.ReadCredential(It.IsAny<string>(), out dummy), Times.Never);
  }

  [Fact]
  public async Task WindowsStore_GetCredential_RethrowsWhenDpapiFails() {
    var mockAdapter = new Mock<IWindowsCredentialAdapter>();
    mockAdapter.Setup(a => a.ReadDpapiSecretAsync("smoc:youtubemusic:cookies", It.IsAny<CancellationToken>()))
               .ThrowsAsync(new System.Security.Cryptography.CryptographicException("DPAPI decryption error"));

    var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
    await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(() =>
        store.GetCredentialAsync("smoc:youtubemusic", "cookies", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task WindowsStore_DeleteCredential_CleansUpBothCredentialManagerAndDpapi() {
    var mockAdapter = new Mock<IWindowsCredentialAdapter>();

    var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
    await store.DeleteCredentialAsync("smoc:youtubemusic", "cookies", TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.DeleteCredential("smoc:youtubemusic:cookies"), Times.Once);
    mockAdapter.Verify(a => a.DeleteDpapiSecret("smoc:youtubemusic:cookies"), Times.Once);
  }

  [Fact]
  public void WindowsStore_DefaultConstructor_ThrowsOnNonWindows() {
    if (!OperatingSystem.IsWindows()) {
      Assert.Throws<PlatformNotSupportedException>(() => new WindowsCredentialManagerCredentialStore());
    }
  }
}
