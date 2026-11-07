using Smoc.Security;
using Smoc.Security.Linux;
using Smoc.Security.MacOs;
using Smoc.Security.Windows;
using Xunit;

namespace smoc.Tests.Security;

public class CredentialStoreFactoryTests {
  [Fact]
  public void Factory_CreateNativeStore_ReturnsStoreForCurrentPlatform() {
    ICredentialStore store = CredentialStoreFactory.CreateNativeStore();
    Assert.NotNull(store);

    if (OperatingSystem.IsLinux()) {
      Assert.IsType<LinuxSecretServiceCredentialStore>(store);
    } else if (OperatingSystem.IsMacOS()) {
      Assert.IsType<MacOsKeychainCredentialStore>(store);
    } else if (OperatingSystem.IsWindows()) {
      Assert.IsType<WindowsCredentialManagerCredentialStore>(store);
    }
  }

  [Fact]
  public void Factory_CreateDefault_ReturnsCompositeStoreWithNativeAndEnv() {
    ICredentialStore defaultStore = CredentialStoreFactory.CreateDefault();
    var composite = Assert.IsType<CompositeCredentialStore>(defaultStore);

    Assert.Equal(2, composite.Stores.Count);
    Assert.IsType<EnvironmentCredentialStore>(composite.Stores[1]);
  }
}
