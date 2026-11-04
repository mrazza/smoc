using System.Runtime.InteropServices;
using System.Text;
using Tmds.DBus;

namespace Smoc.Security;

/// <summary>
/// Internal D-Bus contracts and structures for FreeDesktop Secret Service specification.
/// </summary>
internal static class SecretServiceDbusContracts {
  public const string ServiceName = "org.freedesktop.secrets";
  public const string SchemaName = "org.mrazza.smoc.Credential";
  public static readonly ObjectPath DefaultCollectionPath = new("/org/freedesktop/secrets/aliases/default");
  public static readonly ObjectPath ServicePath = new("/org/freedesktop/secrets");

  [DBusInterface("org.freedesktop.Secret.Service")]
  public interface ISecretService : IDBusObject {
    Task<(object output, ObjectPath result)> OpenSessionAsync(string algorithm, object input);
    Task<(ObjectPath[] unlocked, ObjectPath[] locked)> SearchItemsAsync(IDictionary<string, string> attributes);
    Task<IDictionary<ObjectPath, Secret>> GetSecretsAsync(ObjectPath[] items, ObjectPath session);
    Task<ObjectPath[]> UnlockAsync(ObjectPath[] objects);
  }

  [DBusInterface("org.freedesktop.Secret.Collection")]
  public interface ISecretCollection : IDBusObject {
    Task<(ObjectPath item, ObjectPath prompt)> CreateItemAsync(IDictionary<string, object> properties, Secret secret, bool replace);
    Task<ObjectPath[]> SearchItemsAsync(IDictionary<string, string> attributes);
  }

  [DBusInterface("org.freedesktop.Secret.Item")]
  public interface ISecretItem : IDBusObject {
    Task<Secret> GetSecretAsync(ObjectPath session);
    Task SetSecretAsync(Secret secret);
    Task<ObjectPath> DeleteAsync();
  }

  public struct Secret {
    public ObjectPath Session { get; set; }
    public byte[] Parameters { get; set; }
    public byte[] Value { get; set; }
    public string ContentType { get; set; }
  }
}

/// <summary>
/// Abstraction over FreeDesktop Secret Service communications for unit testability.
/// </summary>
public interface ISecretServiceAdapter {
  /// <summary>
  /// Checks whether the Secret Service daemon is available on the current session bus.
  /// </summary>
  Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Retrieves a secret stored with the given service and account attributes.
  /// </summary>
  Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken);

  /// <summary>
  /// Stores a secret with the given service and account attributes.
  /// </summary>
  Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken);

  /// <summary>
  /// Deletes any secret stored with the given service and account attributes.
  /// </summary>
  Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken);
}

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

/// <summary>
/// An <see cref="ICredentialStore"/> that uses the Linux FreeDesktop Secret Service over D-Bus (GNOME Keyring / KWallet).
/// </summary>
public sealed class LinuxSecretServiceCredentialStore : ICredentialStore {
  private readonly ISecretServiceAdapter _adapter;

  /// <summary>
  /// Initializes a new instance of <see cref="LinuxSecretServiceCredentialStore"/> using the default D-Bus adapter.
  /// </summary>
  public LinuxSecretServiceCredentialStore()
      : this(new DbusSecretServiceAdapter()) {
  }

  /// <summary>
  /// Initializes a new instance of <see cref="LinuxSecretServiceCredentialStore"/> with a custom adapter for testing.
  /// </summary>
  /// <param name="adapter">The secret service adapter.</param>
  public LinuxSecretServiceCredentialStore(ISecretServiceAdapter adapter) {
    _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
  }

  /// <inheritdoc />
  public Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    return _adapter.GetSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);

    return _adapter.SetSecretAsync(service, account, secret, cancellationToken);
  }

  /// <inheritdoc />
  public Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    return _adapter.DeleteSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return _adapter.IsAvailableAsync(cancellationToken);
  }
}
