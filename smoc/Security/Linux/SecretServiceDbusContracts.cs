using Tmds.DBus;

namespace Smoc.Security.Linux;

/// <summary>
/// Internal D-Bus contracts and structures for FreeDesktop Secret Service specification.
/// </summary>
internal static class SecretServiceDbusContracts {
  /// <summary>
  /// The well-known D-Bus bus name for the FreeDesktop Secret Service daemon.
  /// </summary>
  public const string ServiceName = "org.freedesktop.secrets";

  /// <summary>
  /// The XDG schema name identifying credentials created and managed by SMoC.
  /// </summary>
  public const string SchemaName = "org.mrazza.smoc.Credential";

  /// <summary>
  /// The D-Bus object path for the default secret collection alias.
  /// </summary>
  public static readonly ObjectPath DefaultCollectionPath = new("/org/freedesktop/secrets/aliases/default");

  /// <summary>
  /// The D-Bus object path for the main Secret Service entry point.
  /// </summary>
  public static readonly ObjectPath ServicePath = new("/org/freedesktop/secrets");

  /// <summary>
  /// D-Bus proxy interface for the Secret Service daemon (<c>org.freedesktop.Secret.Service</c>).
  /// </summary>
  [DBusInterface("org.freedesktop.Secret.Service")]
  public interface ISecretService : IDBusObject {
    /// <summary>
    /// Opens a new encryption session with the Secret Service.
    /// </summary>
    /// <param name="algorithm">The key agreement algorithm to use (e.g. "plain").</param>
    /// <param name="input">Algorithm-specific parameters or public key.</param>
    /// <returns>A tuple containing output algorithm parameters and the session object path.</returns>
    Task<(object output, ObjectPath result)> OpenSessionAsync(string algorithm, object input);

    /// <summary>
    /// Searches for items matching the specified string attributes.
    /// </summary>
    /// <param name="attributes">The lookup attributes to filter on.</param>
    /// <returns>A tuple containing arrays of unlocked and locked item object paths.</returns>
    Task<(ObjectPath[] unlocked, ObjectPath[] locked)> SearchItemsAsync(IDictionary<string, string> attributes);

    /// <summary>
    /// Retrieves secret values for a collection of item object paths within an open session.
    /// </summary>
    /// <param name="items">The item paths whose secrets should be retrieved.</param>
    /// <param name="session">The active session path.</param>
    /// <returns>A dictionary mapping item paths to their decrypted secret structures.</returns>
    Task<IDictionary<ObjectPath, Secret>> GetSecretsAsync(ObjectPath[] items, ObjectPath session);

    /// <summary>
    /// Unlocks a collection of locked items or collections.
    /// </summary>
    /// <param name="objects">The object paths to unlock.</param>
    /// <returns>An array of object paths that were unlocked.</returns>
    Task<ObjectPath[]> UnlockAsync(ObjectPath[] objects);
  }

  /// <summary>
  /// D-Bus proxy interface for a Secret Service collection (<c>org.freedesktop.Secret.Collection</c>).
  /// </summary>
  [DBusInterface("org.freedesktop.Secret.Collection")]
  public interface ISecretCollection : IDBusObject {
    /// <summary>
    /// Creates a new secret item in this collection.
    /// </summary>
    /// <param name="properties">The D-Bus properties of the item, including label and attributes.</param>
    /// <param name="secret">The secret payload structure.</param>
    /// <param name="replace">Whether to replace an existing item with the same attributes.</param>
    /// <returns>A tuple containing the created item path and an optional prompt path.</returns>
    Task<(ObjectPath item, ObjectPath prompt)> CreateItemAsync(IDictionary<string, object> properties, Secret secret, bool replace);

    /// <summary>
    /// Searches for items in this collection matching the specified attributes.
    /// </summary>
    /// <param name="attributes">The lookup attributes to filter on.</param>
    /// <returns>An array of item object paths matching the attributes.</returns>
    Task<ObjectPath[]> SearchItemsAsync(IDictionary<string, string> attributes);
  }

  /// <summary>
  /// D-Bus proxy interface for an individual Secret Service item (<c>org.freedesktop.Secret.Item</c>).
  /// </summary>
  [DBusInterface("org.freedesktop.Secret.Item")]
  public interface ISecretItem : IDBusObject {
    /// <summary>
    /// Retrieves the secret value for this item in the specified session.
    /// </summary>
    /// <param name="session">The active session path.</param>
    /// <returns>The decrypted secret payload structure.</returns>
    Task<Secret> GetSecretAsync(ObjectPath session);

    /// <summary>
    /// Updates the secret value stored in this item.
    /// </summary>
    /// <param name="secret">The new secret payload structure.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetSecretAsync(Secret secret);

    /// <summary>
    /// Deletes this item from the collection.
    /// </summary>
    /// <returns>An object path of a prompt if required, or an empty path.</returns>
    Task<ObjectPath> DeleteAsync();
  }

  /// <summary>
  /// Represents a secret payload passed across the FreeDesktop Secret Service D-Bus interface.
  /// </summary>
  public struct Secret {
    /// <summary>
    /// Gets or sets the D-Bus object path of the session used to transfer this secret.
    /// </summary>
    public ObjectPath Session { get; set; }

    /// <summary>
    /// Gets or sets algorithm-specific parameters (e.g. initialization vector for encrypted sessions).
    /// </summary>
    public byte[] Parameters { get; set; }

    /// <summary>
    /// Gets or sets the byte array representing the secret data.
    /// </summary>
    public byte[] Value { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type of the secret data (typically "text/plain; charset=utf-8").
    /// </summary>
    public string ContentType { get; set; }
  }
}
