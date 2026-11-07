namespace Smoc.Security;

/// <summary>
/// A credential store that delegates operations across multiple underlying stores in priority order.
/// </summary>
public sealed class CompositeCredentialStore : ICredentialStore {
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

    foreach (ICredentialStore store in Stores) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        if (await store.IsAvailableAsync(cancellationToken)) {
          string? credential = await store.GetCredentialAsync(service, account, cancellationToken);
          if (!string.IsNullOrEmpty(credential)) {
            return credential;
          }
        }
      } catch (OperationCanceledException) {
        throw;
      } catch {
        // Fall through to next available store
      }
    }

    return null;
  }

  /// <inheritdoc />
  public async Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);

    foreach (ICredentialStore store in Stores) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        if (await store.IsAvailableAsync(cancellationToken)) {
          await store.SetCredentialAsync(service, account, secret, cancellationToken);
          return;
        }
      } catch (OperationCanceledException) {
        throw;
      } catch {
        // Try next store if primary failed
      }
    }

    throw new InvalidOperationException($"Failed to save credential for service '{service}' and account '{account}': no available credential store succeeded.");
  }

  /// <inheritdoc />
  public async Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    bool deleted = false;
    foreach (ICredentialStore store in Stores) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        if (await store.IsAvailableAsync(cancellationToken)) {
          await store.DeleteCredentialAsync(service, account, cancellationToken);
          deleted = true;
        }
      } catch (OperationCanceledException) {
        throw;
      } catch {
        // Continue deleting from remaining stores
      }
    }

    if (!deleted) {
      throw new InvalidOperationException($"Failed to delete credential for service '{service}' and account '{account}': no available credential store succeeded.");
    }
  }

  /// <inheritdoc />
  public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    foreach (ICredentialStore store in Stores) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        if (await store.IsAvailableAsync(cancellationToken)) {
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
}
