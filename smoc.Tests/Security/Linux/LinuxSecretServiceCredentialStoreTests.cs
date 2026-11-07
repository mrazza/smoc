using Moq;
using Smoc.Security;
using Smoc.Security.Linux;
using Tmds.DBus;
using Xunit;

namespace smoc.Tests.Security.Linux;

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

  [Fact]
  public void SecretStruct_HasSequentialLayout() {
    Assert.True(typeof(SecretServiceDbusContracts.Secret).IsLayoutSequential);
  }

  [Fact]
  public void DbusConnection_CanCreateProxiesForInternalSecretServiceContracts() {
    using var connection = new Connection(Address.Session ?? "tcp:host=localhost,port=1234");

    var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
        SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);
    Assert.NotNull(serviceProxy);

    var collectionProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretCollection>(
        SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.DefaultCollectionPath);
    Assert.NotNull(collectionProxy);

    var itemProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretItem>(
        SecretServiceDbusContracts.ServiceName, new ObjectPath("/org/freedesktop/secrets/item/test"));
    Assert.NotNull(itemProxy);
  }

  [Fact]
  public void ResolveSessionBusAddress_ReturnsEnvironmentAddress_WhenPresent() {
    string? address = DbusSecretServiceAdapter.ResolveSessionBusAddress(
        getEnv: key => key == "DBUS_SESSION_BUS_ADDRESS" ? "unix:path=/custom/bus" : null);
    Assert.Equal("unix:path=/custom/bus", address);
  }

  [Fact]
  public void ResolveSessionBusAddress_FallsBackToXdgRuntimeDir_WhenBusAddressUnsetAndBusExists() {
    string? address = DbusSecretServiceAdapter.ResolveSessionBusAddress(
        getEnv: key => key == "XDG_RUNTIME_DIR" ? "/run/user/1000" : null,
        pathExists: path => path == Path.Combine("/run/user/1000", "bus"));

    Assert.Equal($"unix:path={Path.Combine("/run/user/1000", "bus")}", address);
  }

  [Fact]
  public void ResolveSessionBusAddress_ReturnsNull_WhenEnvironmentUnsetOrBusMissing() {
    string? unsetXdg = DbusSecretServiceAdapter.ResolveSessionBusAddress(
        getEnv: _ => null,
        pathExists: _ => true);
    Assert.Null(unsetXdg);

    string? missingBus = DbusSecretServiceAdapter.ResolveSessionBusAddress(
        getEnv: key => key == "XDG_RUNTIME_DIR" ? "/run/user/1000" : null,
        pathExists: _ => false);
    Assert.Null(missingBus);
  }
}
