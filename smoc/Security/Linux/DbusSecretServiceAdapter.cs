using System.Runtime.InteropServices;
using System.Text;
using Tmds.DBus;

namespace Smoc.Security.Linux;

/// <summary>
/// Default implementation of <see cref="ISecretServiceAdapter"/> using <see cref="Connection.Session"/>.
/// </summary>
internal sealed class DbusSecretServiceAdapter : ISecretServiceAdapter {
  private Connection? _connection;
  private ObjectPath? _sessionPath;

  private async Task<Connection?> GetConnectionAsync(CancellationToken cancellationToken) {
    if (_connection != null) {
      return _connection;
    }

    try {
      var connection = new Connection(Address.Session);
      await connection.ConnectAsync();
      _connection = connection;
      return _connection;
    } catch {
      return null;
    }
  }

  private async Task<ObjectPath?> GetSessionPathAsync(Connection connection, SecretServiceDbusContracts.ISecretService serviceProxy) {
    if (_sessionPath.HasValue) {
      return _sessionPath.Value;
    }

    try {
      var (_, result) = await serviceProxy.OpenSessionAsync("plain", string.Empty);
      _sessionPath = result;
      return _sessionPath;
    } catch {
      return null;
    }
  }

  public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken) {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
      return false;
    }

    try {
      Connection? connection = await GetConnectionAsync(cancellationToken);
      if (connection == null) {
        return false;
      }

      return await connection.IsServiceActiveAsync(SecretServiceDbusContracts.ServiceName);
    } catch {
      return false;
    }
  }

  public async Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken) {
    Connection? connection = await GetConnectionAsync(cancellationToken);
    if (connection == null) {
      return null;
    }

    var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
        SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);

    ObjectPath? session = await GetSessionPathAsync(connection, serviceProxy);
    if (!session.HasValue) {
      return null;
    }

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

    var secrets = await serviceProxy.GetSecretsAsync(items, session.Value);
    if (secrets.TryGetValue(items[0], out var secret) && secret.Value != null) {
      return Encoding.UTF8.GetString(secret.Value);
    }

    return null;
  }

  public async Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken) {
    Connection? connection = await GetConnectionAsync(cancellationToken);
    if (connection == null) {
      throw new InvalidOperationException("Secret Service D-Bus connection is not available.");
    }

    var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
        SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);

    ObjectPath? session = await GetSessionPathAsync(connection, serviceProxy);
    if (!session.HasValue) {
      throw new InvalidOperationException("Failed to open Secret Service session.");
    }

    var collectionProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretCollection>(
        SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.DefaultCollectionPath);

    var attributes = CreateAttributes(service, account);
    var properties = new Dictionary<string, object> {
      ["org.freedesktop.Secret.Item.Label"] = $"SMoC: {service} ({account})",
      ["org.freedesktop.Secret.Item.Attributes"] = attributes
    };

    var secretStruct = new SecretServiceDbusContracts.Secret {
      Session = session.Value,
      Parameters = Array.Empty<byte>(),
      Value = Encoding.UTF8.GetBytes(secret),
      ContentType = "text/plain; charset=utf-8"
    };

    await collectionProxy.CreateItemAsync(properties, secretStruct, replace: true);
  }

  public async Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken) {
    Connection? connection = await GetConnectionAsync(cancellationToken);
    if (connection == null) {
      return;
    }

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
