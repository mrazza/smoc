using Moq;
using Smoc.Security;
using Xunit;

namespace smoc.Tests.Security;

public class WindowsCredentialManagerCredentialStoreTests {
  [Fact]
  public async Task WindowsStore_SmallCredential_WritesToCredentialManager() {
    string tempDir = Path.Combine(Path.GetTempPath(), "smoc_test_win_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(tempDir);
    try {
      var mockAdapter = new Mock<IWindowsCredentialAdapter>();
      mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
      mockAdapter.SetupGet(a => a.CredentialsDirectory).Returns(tempDir);
      mockAdapter.Setup(a => a.WriteCredential(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                 .Returns(true);

      var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
      string smallSecret = "standard_password";

      await store.SetCredentialAsync("smoc:subsonic", "razza", smallSecret, TestContext.Current.CancellationToken);

      mockAdapter.Verify(a => a.WriteCredential("smoc:subsonic:razza", "razza", smallSecret), Times.Once);
      mockAdapter.Verify(a => a.ProtectData(It.IsAny<byte[]>()), Times.Never);
    } finally {
      if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task WindowsStore_LargePayload_WritesToDpapiFile() {
    string tempDir = Path.Combine(Path.GetTempPath(), "smoc_test_win_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(tempDir);
    try {
      var mockAdapter = new Mock<IWindowsCredentialAdapter>();
      mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
      mockAdapter.SetupGet(a => a.CredentialsDirectory).Returns(tempDir);
      mockAdapter.Setup(a => a.ProtectData(It.IsAny<byte[]>()))
                 .Returns<byte[]>(b => b); // Identity "encryption" for test

      var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
      // Large secret > 2048 bytes (e.g. 3500 bytes YTM cookie string)
      string largeCookies = new('A', 3500);

      await store.SetCredentialAsync("smoc:youtubemusic", "cookies", largeCookies, TestContext.Current.CancellationToken);

      mockAdapter.Verify(a => a.ProtectData(It.IsAny<byte[]>()), Times.Once);
      mockAdapter.Verify(a => a.WriteCredential(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
      mockAdapter.Verify(a => a.DeleteCredential("smoc:youtubemusic:cookies"), Times.Once);

      // Verify file was written to credentials directory
      string[] files = Directory.GetFiles(tempDir, "*.dat");
      Assert.Single(files);
    } finally {
      if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task WindowsStore_GetCredential_PrefersDpapiFileWhenPresent() {
    string tempDir = Path.Combine(Path.GetTempPath(), "smoc_test_win_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(tempDir);
    try {
      var mockAdapter = new Mock<IWindowsCredentialAdapter>();
      mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
      mockAdapter.SetupGet(a => a.CredentialsDirectory).Returns(tempDir);
      mockAdapter.Setup(a => a.ProtectData(It.IsAny<byte[]>()))
                 .Returns<byte[]>(b => b);
      mockAdapter.Setup(a => a.UnprotectData(It.IsAny<byte[]>()))
                 .Returns<byte[]>(b => b);

      var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
      string largeCookies = new('C', 2500);

      await store.SetCredentialAsync("smoc:youtubemusic", "cookies", largeCookies, TestContext.Current.CancellationToken);

      string? retrieved = await store.GetCredentialAsync("smoc:youtubemusic", "cookies", TestContext.Current.CancellationToken);
      Assert.Equal(largeCookies, retrieved);
      mockAdapter.Verify(a => a.UnprotectData(It.IsAny<byte[]>()), Times.Once);
    } finally {
      if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task WindowsStore_DeleteCredential_CleansUpBothCredentialManagerAndDpapi() {
    string tempDir = Path.Combine(Path.GetTempPath(), "smoc_test_win_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(tempDir);
    try {
      var mockAdapter = new Mock<IWindowsCredentialAdapter>();
      mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
      mockAdapter.SetupGet(a => a.CredentialsDirectory).Returns(tempDir);
      mockAdapter.Setup(a => a.ProtectData(It.IsAny<byte[]>())).Returns<byte[]>(b => b);

      var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
      await store.SetCredentialAsync("smoc:youtubemusic", "cookies", new string('X', 3000), TestContext.Current.CancellationToken);

      await store.DeleteCredentialAsync("smoc:youtubemusic", "cookies", TestContext.Current.CancellationToken);

      mockAdapter.Verify(a => a.DeleteCredential("smoc:youtubemusic:cookies"), Times.AtLeastOnce());
      string[] files = Directory.GetFiles(tempDir, "*.dat");
      Assert.Empty(files);
    } finally {
      if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
  }
}
