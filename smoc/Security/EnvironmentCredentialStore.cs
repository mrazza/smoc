using System.Text.RegularExpressions;

namespace Smoc.Security;

/// <summary>
/// An <see cref="ICredentialStore"/> that resolves credentials from environment variables.
/// Ideal for headless, CI, and containerized environments.
/// </summary>
public partial class EnvironmentCredentialStore : ICredentialStore {
  private readonly Func<string, string?> _getEnv;
  private readonly Action<string, string?>? _setEnv;

  /// <summary>
  /// Initializes a new instance of <see cref="EnvironmentCredentialStore"/> using system environment variables.
  /// </summary>
  public EnvironmentCredentialStore()
      : this(Environment.GetEnvironmentVariable, Environment.SetEnvironmentVariable) {
  }

  /// <summary>
  /// Initializes a new instance of <see cref="EnvironmentCredentialStore"/> with custom environment accessors for testability.
  /// </summary>
  /// <param name="getEnv">Function to read environment variable values.</param>
  /// <param name="setEnv">Optional action to set or clear environment variable values.</param>
  public EnvironmentCredentialStore(Func<string, string?> getEnv, Action<string, string?>? setEnv = null) {
    _getEnv = getEnv ?? throw new ArgumentNullException(nameof(getEnv));
    _setEnv = setEnv;
  }

  /// <inheritdoc />
  public async Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    foreach (string varName in GetCandidateVariableNames(service, account)) {
      string? value = _getEnv(varName);
      if (!string.IsNullOrEmpty(value)) {
        return value;
      }
    }

    return null;
  }

  /// <inheritdoc />
  public async Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);

    if (_setEnv == null) {
      return;
    }

    string primaryVar = GetCandidateVariableNames(service, account).First();
    _setEnv(primaryVar, secret);
  }

  /// <inheritdoc />
  public async Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    if (_setEnv == null) {
      return;
    }

    foreach (string varName in GetCandidateVariableNames(service, account)) {
      _setEnv(varName, null);
    }
  }

  /// <inheritdoc />
  public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return true;
  }

  /// <summary>
  /// Yields potential environment variable names in priority order for the given service and account.
  /// </summary>
  public static IEnumerable<string> GetCandidateVariableNames(string service, string account) {
    if (string.Equals(service, CredentialTargets.Subsonic, StringComparison.OrdinalIgnoreCase)) {
      yield return $"SMOC_SUBSONIC_{Sanitize(account)}_PASSWORD";
      yield return "SMOC_SUBSONIC_PASSWORD";
    } else if (string.Equals(service, CredentialTargets.SoundCloud, StringComparison.OrdinalIgnoreCase)) {
      if (string.Equals(account, CredentialTargets.AccountClientId, StringComparison.OrdinalIgnoreCase)) {
        yield return "SMOC_SOUNDCLOUD_CLIENT_ID";
      } else {
        yield return "SMOC_SOUNDCLOUD_AUTH_TOKEN";
      }
    } else if (string.Equals(service, CredentialTargets.YouTubeMusic, StringComparison.OrdinalIgnoreCase)) {
      if (string.Equals(account, CredentialTargets.AccountCookies, StringComparison.OrdinalIgnoreCase)) {
        yield return "SMOC_YTM_COOKIES";
      } else if (string.Equals(account, CredentialTargets.AccountTokens, StringComparison.OrdinalIgnoreCase)) {
        yield return "SMOC_YTM_TOKENS";
      }
    }

    yield return $"SMOC_CREDENTIAL_{Sanitize(service)}_{Sanitize(account)}";
  }

  private static string Sanitize(string input) {
    return NonAlphaNumericRegex().Replace(input, "_").ToUpperInvariant().Trim('_');
  }

  [GeneratedRegex(@"[^a-zA-Z0-9]+")]
  private static partial Regex NonAlphaNumericRegex();
}
