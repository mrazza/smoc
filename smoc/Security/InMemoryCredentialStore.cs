using System.Collections.Concurrent;

namespace Smoc.Security;

/// <summary>
/// An in-memory implementation of <see cref="ICredentialStore"/> useful for testing and transient sessions.
/// </summary>
public sealed class InMemoryCredentialStore : ICredentialStore {
  private readonly ConcurrentDictionary<(string Service, string Account), string> _store = new();

  /// <inheritdoc />
  public Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    _store.TryGetValue((service, account), out string? secret);
    return Task.FromResult(secret);
  }

  /// <inheritdoc />
  public Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);

    _store[(service, account)] = secret;
    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    _store.TryRemove((service, account), out _);
    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(true);
  }
}
