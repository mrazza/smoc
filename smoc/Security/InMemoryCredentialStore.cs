using System.Collections.Concurrent;

namespace Smoc.Security;

/// <summary>
/// An in-memory implementation of <see cref="ICredentialStore"/> useful for testing and transient sessions.
/// </summary>
public sealed class InMemoryCredentialStore : ICredentialStore {
  private readonly ConcurrentDictionary<(string Service, string Account), string> _store = new();

  /// <inheritdoc />
  public async Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    _store.TryGetValue((service, account), out string? secret);
    return secret;
  }

  /// <inheritdoc />
  public async Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);

    _store[(service, account)] = secret;
  }

  /// <inheritdoc />
  public async Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    _store.TryRemove((service, account), out _);
  }

  /// <inheritdoc />
  public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return true;
  }
}
