using Tmds.DBus;

namespace Smoc.Security.Linux;

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
