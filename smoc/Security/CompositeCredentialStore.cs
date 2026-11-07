using System.Collections.Concurrent;

namespace Smoc.Security;

/// <summary>
/// A credential store that delegates operations across multiple underlying stores in priority order.
/// </summary>
public sealed class CompositeCredentialStore : ICredentialStore {
  private readonly ConcurrentDictionary<ICredentialStore, Task<bool>> _availabilityCache = new();

  /// <summary>
  /// Initializes a new instance of <see cref="CompositeCredentialStore"/> with the provided stores in order of precedence.
  /// </summary>
  /// <param name="stores">The list of credential stores to consult in order.</param>
  public CompositeCredentialStore(params ICredentialStore[] stores)
      : this((IReadOnlyList<ICredentialStore>)stores) {
  }

  /// <summary>
  /// Initializes a new instance of <see cref="CompositeCredentialStore"/> with the provided stores in order of precedence.
  /// </summary>
  /// <param name="stores">The list of credential stores to consult in order.</param>
  public CompositeCredentialStore(IReadOnlyList<ICredentialStore> stores) {
    ArgumentNullException.ThrowIfNull(stores);
    if (stores.Count == 0) {
      throw new ArgumentException("At least one credential store must be provided.", nameof(stores));
    }

    Stores = stores;
  }

  /// <summary>
  /// Gets the stores contained in this composite store.
  /// </summary>
  public IReadOnlyList<ICredentialStore> Stores { get; private set; }

  /// <inheritdoc />
  public async Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    cancellationToken.ThrowIfCancellationRequested();

    List<Exception>? exceptions = null;

    foreach (ICredentialStore store in Stores) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        if (await IsStoreAvailableCachedAsync(store, cancellationToken)) {
          string? credential = await store.GetCredentialAsync(service, account, cancellationToken);
          if (!string.IsNullOrEmpty(credential)) {
            return credential;
          }
        }
      } catch (OperationCanceledException) {
        throw;
      } catch (Exception ex) {
        (exceptions ??= new List<Exception>()).Add(ex);
      }
    }

    if (exceptions != null && exceptions.Count > 0) {
      throw BuildFailure("retrieve", service, account, exceptions);
    }

    return null;
  }

  /// <inheritdoc />
  public async Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);
    cancellationToken.ThrowIfCancellationRequested();

    List<Exception>? exceptions = null;

    foreach (ICredentialStore store in Stores) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        if (await IsStoreAvailableCachedAsync(store, cancellationToken)) {
          await store.SetCredentialAsync(service, account, secret, cancellationToken);
          return;
        }
      } catch (OperationCanceledException) {
        throw;
      } catch (NotSupportedException) {
        // Skip read-only stores without masking operational store failures
        continue;
      } catch (Exception ex) {
        (exceptions ??= new List<Exception>()).Add(ex);
      }
    }

    if (exceptions != null && exceptions.Count > 0) {
      throw BuildFailure("save", service, account, exceptions);
    }

    throw new InvalidOperationException($"Failed to save credential for service '{service}' and account '{account}': no available credential store succeeded.");
  }

  /// <inheritdoc />
  public async Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    cancellationToken.ThrowIfCancellationRequested();

    bool deleted = false;
    List<Exception>? exceptions = null;

    foreach (ICredentialStore store in Stores) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        if (await IsStoreAvailableCachedAsync(store, cancellationToken)) {
          await store.DeleteCredentialAsync(service, account, cancellationToken);
          deleted = true;
        }
      } catch (OperationCanceledException) {
        throw;
      } catch (NotSupportedException) {
        // Skip read-only stores without masking operational store failures
        continue;
      } catch (Exception ex) {
        (exceptions ??= new List<Exception>()).Add(ex);
      }
    }

    if (!deleted) {
      if (exceptions != null && exceptions.Count > 0) {
        throw BuildFailure("delete", service, account, exceptions);
      }

      throw new InvalidOperationException($"Failed to delete credential for service '{service}' and account '{account}': no available credential store succeeded.");
    }
  }

  /// <inheritdoc />
  public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();

    foreach (ICredentialStore store in Stores) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        if (await IsStoreAvailableCachedAsync(store, cancellationToken)) {
          return true;
        }
      } catch (OperationCanceledException) {
        throw;
      } catch {
        // Check next store
      }
    }

    return false;
  }

  private Task<bool> IsStoreAvailableCachedAsync(ICredentialStore store, CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();
    return _availabilityCache.GetOrAdd(store, s => s.IsAvailableAsync(CancellationToken.None));
  }

  private static Exception BuildFailure(string verb, string service, string account, IReadOnlyList<Exception> exceptions) {
    if (exceptions.Count == 1) {
      return new InvalidOperationException($"Failed to {verb} credential for service '{service}' and account '{account}'.", exceptions[0]);
    }

    return new AggregateException($"Failed to {verb} credential for service '{service}' and account '{account}': all attempted credential stores failed.", exceptions);
  }
}
