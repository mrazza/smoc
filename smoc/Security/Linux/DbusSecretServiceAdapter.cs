using System.Runtime.InteropServices;
using System.Text;
using Tmds.DBus;

namespace Smoc.Security.Linux;

/// <summary>
/// Default implementation of <see cref="ISecretServiceAdapter"/> using FreeDesktop Secret Service over D-Bus.
/// </summary>
internal sealed class DbusSecretServiceAdapter : ISecretServiceAdapter, IDisposable {
  private readonly SemaphoreSlim _gate = new(1, 1);
  private Connection? _connection;
  private ObjectPath? _sessionPath;
  private bool _disposed;

  /// <summary>
  /// Initializes a new instance of the <see cref="DbusSecretServiceAdapter"/> class.
  /// </summary>
  public DbusSecretServiceAdapter() {
  }

  /// <inheritdoc />
  public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken) {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
      return false;
    }

    try {
      Connection connection = await GetConnectionAsync(cancellationToken);
      if (await connection.IsServiceActiveAsync(SecretServiceDbusContracts.ServiceName)) {
        return true;
      }

      // If not yet active, attempt to open a session to trigger D-Bus activation (e.g. gnome-keyring / KWallet)
      await GetSessionPathAsync(connection, cancellationToken);
      return true;
    } catch {
      return false;
    }
  }

  /// <inheritdoc />
  public async Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken) {
    try {
      Connection connection = await GetConnectionAsync(cancellationToken);
      ObjectPath session = await GetSessionPathAsync(connection, cancellationToken);

      var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
          SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);

      var attributes = CreateAttributes(service, account);
      var (unlocked, locked) = await serviceProxy.SearchItemsAsync(attributes);

      ObjectPath[] items = unlocked;
      if (items.Length == 0 && locked.Length > 0) {
        var (unlockedItems, _) = await serviceProxy.UnlockAsync(locked);
        items = unlockedItems;
      }

      if (items.Length == 0) {
        return null;
      }

      var secrets = await serviceProxy.GetSecretsAsync(items, session);
      if (secrets.TryGetValue(items[0], out var secret) && secret.Value != null) {
        return Encoding.UTF8.GetString(secret.Value);
      }

      return null;
    } catch {
      ResetSession();
      throw;
    }
  }

  /// <inheritdoc />
  public async Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken) {
    try {
      Connection connection = await GetConnectionAsync(cancellationToken);
      ObjectPath session = await GetSessionPathAsync(connection, cancellationToken);

      var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
          SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);
      await serviceProxy.UnlockAsync([SecretServiceDbusContracts.DefaultCollectionPath]);

      var collectionProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretCollection>(
          SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.DefaultCollectionPath);

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

      var (item, _) = await collectionProxy.CreateItemAsync(properties, secretStruct, replace: true);
      if (item == new ObjectPath("/")) {
        throw new InvalidOperationException($"Failed to create secret item for service '{service}' and account '{account}': keyring collection is locked or rejected item creation.");
      }
    } catch {
      ResetSession();
      throw;
    }
  }

  /// <inheritdoc />
  public async Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken) {
    try {
      Connection connection = await GetConnectionAsync(cancellationToken);

      var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
          SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);

      var attributes = CreateAttributes(service, account);
      var (unlocked, locked) = await serviceProxy.SearchItemsAsync(attributes);

      foreach (ObjectPath itemPath in unlocked.Concat(locked)) {
        var itemProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretItem>(
            SecretServiceDbusContracts.ServiceName, itemPath);
        await itemProxy.DeleteAsync();
      }
    } catch {
      ResetSession();
      throw;
    }
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _disposed = true;
    _connection?.Dispose();
    _connection = null;
    _sessionPath = null;
    _gate.Dispose();
  }

  internal static string? ResolveSessionBusAddress(
      Func<string, string?>? getEnv = null,
      Func<string, bool>? pathExists = null) {
    getEnv ??= Environment.GetEnvironmentVariable;
    pathExists ??= Path.Exists;

    string? address = getEnv("DBUS_SESSION_BUS_ADDRESS");
    if (!string.IsNullOrEmpty(address)) {
      return address;
    }

    string? xdgRuntimeDir = getEnv("XDG_RUNTIME_DIR");
    if (!string.IsNullOrEmpty(xdgRuntimeDir)) {
      string busPath = Path.Combine(xdgRuntimeDir, "bus");
      if (pathExists(busPath)) {
        return $"unix:path={busPath}";
      }
    }

    return null;
  }

  private async Task<Connection> GetConnectionAsync(CancellationToken cancellationToken) {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (_connection != null) {
      return _connection;
    }

    await _gate.WaitAsync(cancellationToken);
    try {
      if (_connection != null) {
        return _connection;
      }

      string? address = ResolveSessionBusAddress();
      if (address == null) {
        throw new InvalidOperationException("Unable to determine D-Bus session bus address. Ensure DBUS_SESSION_BUS_ADDRESS or XDG_RUNTIME_DIR is set.");
      }

      var connection = new Connection(address);
      try {
        await connection.ConnectAsync();
        _connection = connection;
        return connection;
      } catch {
        connection.Dispose();
        throw;
      }
    } finally {
      _gate.Release();
    }
  }

  private async Task<ObjectPath> GetSessionPathAsync(Connection connection, CancellationToken cancellationToken) {
    if (_sessionPath != null) {
      return _sessionPath.Value;
    }

    await _gate.WaitAsync(cancellationToken);
    try {
      if (_sessionPath != null) {
        return _sessionPath.Value;
      }

      var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
          SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);
      var (_, result) = await serviceProxy.OpenSessionAsync("plain", string.Empty);
      _sessionPath = result;
      return result;
    } catch {
      _sessionPath = null;
      throw;
    } finally {
      _gate.Release();
    }
  }

  private void ResetSession() {
    _sessionPath = null;
  }

  private static Dictionary<string, string> CreateAttributes(string service, string account) {
    return new Dictionary<string, string> {
      ["service"] = service,
      ["account"] = account,
      ["xdg:schema"] = SecretServiceDbusContracts.SchemaName
    };
  }
}
