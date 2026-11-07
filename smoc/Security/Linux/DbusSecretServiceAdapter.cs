using System.Runtime.InteropServices;
using System.Text;
using Tmds.DBus;

namespace Smoc.Security.Linux;

/// <summary>
/// Default implementation of <see cref="ISecretServiceAdapter"/> using <see cref="Connection.Session"/>.
/// </summary>
internal sealed class DbusSecretServiceAdapter : ISecretServiceAdapter {
  private readonly Lazy<Task<Connection>> _connectionLazy;
  private readonly Lazy<Task<ObjectPath>> _sessionPathLazy;

  public DbusSecretServiceAdapter() {
    _connectionLazy = new Lazy<Task<Connection>>(async () => {
      var connection = new Connection(Address.Session);
      await connection.ConnectAsync();
      return connection;
    });

    _sessionPathLazy = new Lazy<Task<ObjectPath>>(async () => {
      Connection connection = await _connectionLazy.Value;
      var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
          SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);
      var (_, result) = await serviceProxy.OpenSessionAsync("plain", string.Empty);
      return result;
    });
  }

  /// <inheritdoc />
  public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken) {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
      return false;
    }

    try {
      Connection connection = await _connectionLazy.Value.WaitAsync(cancellationToken);
      return await connection.IsServiceActiveAsync(SecretServiceDbusContracts.ServiceName);
    } catch {
      return false;
    }
  }

  /// <inheritdoc />
  public async Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken) {
    Connection connection = await _connectionLazy.Value.WaitAsync(cancellationToken);

    var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
        SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);

    ObjectPath session = await _sessionPathLazy.Value.WaitAsync(cancellationToken);

    var attributes = CreateAttributes(service, account);
    var (unlocked, locked) = await serviceProxy.SearchItemsAsync(attributes);

    ObjectPath[] items = unlocked;
    if (items.Length == 0 && locked.Length > 0) {
      await serviceProxy.UnlockAsync(locked);
      items = locked;
    }

    if (items.Length == 0) {
      return null;
    }

    var secrets = await serviceProxy.GetSecretsAsync(items, session);
    if (secrets.TryGetValue(items[0], out var secret) && secret.Value != null) {
      return Encoding.UTF8.GetString(secret.Value);
    }

    return null;
  }

  /// <inheritdoc />
  public async Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken) {
    Connection connection = await _connectionLazy.Value.WaitAsync(cancellationToken);

    var collectionProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretCollection>(
        SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.DefaultCollectionPath);

    ObjectPath session = await _sessionPathLazy.Value.WaitAsync(cancellationToken);

    var attributes = CreateAttributes(service, account);
    var properties = new Dictionary<string, object> {
      ["org.freedesktop.Secret.Item.Label"] = $"SMoC: {service} ({account})",
      ["org.freedesktop.Secret.Item.Attributes"] = attributes
    };

    var secretStruct = new SecretServiceDbusContracts.Secret {
      Session = session,
      Parameters = Array.Empty<byte>(),
      Value = Encoding.UTF8.GetBytes(secret),
      ContentType = "text/plain; charset=utf-8"
    };

    await collectionProxy.CreateItemAsync(properties, secretStruct, replace: true);
  }

  /// <inheritdoc />
  public async Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken) {
    Connection connection = await _connectionLazy.Value.WaitAsync(cancellationToken);

    var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
        SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);

    var attributes = CreateAttributes(service, account);
    var (unlocked, locked) = await serviceProxy.SearchItemsAsync(attributes);

    foreach (ObjectPath itemPath in unlocked.Concat(locked)) {
      var itemProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretItem>(
          SecretServiceDbusContracts.ServiceName, itemPath);
      await itemProxy.DeleteAsync();
    }
  }

  private static Dictionary<string, string> CreateAttributes(string service, string account) {
    return new Dictionary<string, string> {
      ["service"] = service,
      ["account"] = account,
      ["xdg:schema"] = SecretServiceDbusContracts.SchemaName
    };
  }
}
