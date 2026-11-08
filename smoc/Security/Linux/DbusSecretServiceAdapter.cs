using System.Runtime.InteropServices;
using System.Text;
using Tmds.DBus;

namespace Smoc.Security.Linux;

/// <summary>
/// Default implementation of <see cref="ISecretServiceAdapter"/> using FreeDesktop Secret Service over D-Bus.
/// </summary>
internal sealed class DbusSecretServiceAdapter : ISecretServiceAdapter, IDisposable {
  private static readonly ObjectPath NoPromptPath = new("/");

  private readonly SemaphoreSlim _gate = new(1, 1);
  private Connection? _connection;
  private ObjectPath? _sessionPath;
  private bool _disposed;

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
    } catch (OperationCanceledException) {
      throw;
    } catch {
      ResetConnectionAndSession();
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
        var (unlockedItems, prompt) = await serviceProxy.UnlockAsync(locked);
        if (unlockedItems.Length == 0 && IsPromptRequired(prompt)) {
          throw new InvalidOperationException(
              $"Failed to retrieve secret for service '{service}' and account '{account}': the Secret Service keyring is locked.");
        }

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
      ResetConnectionAndSession();
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
      var (unlockedCollections, unlockPrompt) = await serviceProxy.UnlockAsync([SecretServiceDbusContracts.DefaultCollectionPath]);
      if (unlockedCollections.Length == 0 && IsPromptRequired(unlockPrompt)) {
        throw new InvalidOperationException(
            $"Failed to save secret for service '{service}' and account '{account}': the Secret Service keyring collection is locked.");
      }

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

      var (item, createPrompt) = await collectionProxy.CreateItemAsync(properties, secretStruct, replace: true);
      if (item == NoPromptPath || IsPromptRequired(createPrompt)) {
        throw new InvalidOperationException(
            $"Failed to create secret item for service '{service}' and account '{account}': keyring collection is locked or rejected item creation.");
      }
    } catch {
      ResetConnectionAndSession();
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

      ObjectPath[] unlockedLocked = Array.Empty<ObjectPath>();
      if (locked.Length > 0) {
        var (unlockedItems, unlockPrompt) = await serviceProxy.UnlockAsync(locked);
        if (unlockedItems.Length < locked.Length && IsPromptRequired(unlockPrompt)) {
          throw new InvalidOperationException(
              $"Failed to delete secret for service '{service}' and account '{account}': the Secret Service keyring is locked.");
        }

        unlockedLocked = unlockedItems;
      }

      foreach (ObjectPath itemPath in unlocked.Concat(unlockedLocked)) {
        var itemProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretItem>(
            SecretServiceDbusContracts.ServiceName, itemPath);
        ObjectPath deletePrompt = await itemProxy.DeleteAsync();
        if (IsPromptRequired(deletePrompt)) {
          throw new InvalidOperationException(
              $"Failed to delete secret for service '{service}' and account '{account}': the Secret Service keyring is locked.");
        }
      }
    } catch {
      ResetConnectionAndSession();
      throw;
    }
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _disposed = true;
    ResetConnectionAndSession();
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

    Connection? existing = _connection;
    if (existing != null) {
      return existing;
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
    ObjectPath? existing = _sessionPath;
    if (existing != null) {
      return existing.Value;
    }

    await _gate.WaitAsync(cancellationToken);
    try {
      if (_sessionPath != null) {
        return _sessionPath.Value;
      }

      var serviceProxy = connection.CreateProxy<SecretServiceDbusContracts.ISecretService>(
          SecretServiceDbusContracts.ServiceName, SecretServiceDbusContracts.ServicePath);
      // Use "plain" session transport over the local same-UID UNIX domain socket session bus.
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

  private void ResetConnectionAndSession() {
    _sessionPath = null;
    Connection? conn = Interlocked.Exchange(ref _connection, null);
    conn?.Dispose();
  }

  private static bool IsPromptRequired(ObjectPath prompt) {
    string path = prompt.ToString();
    return !string.IsNullOrEmpty(path) && prompt != NoPromptPath;
  }

  private static Dictionary<string, string> CreateAttributes(string service, string account) {
    return new Dictionary<string, string> {
      ["service"] = service,
      ["account"] = account,
      ["xdg:schema"] = SecretServiceDbusContracts.SchemaName
    };
  }
}
