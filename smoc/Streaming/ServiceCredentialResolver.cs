using System.Net;
using System.Text.Json;
using Smoc.Configuration;
using Smoc.Security;
using Smoc.Streaming.SoundCloud;
using Smoc.Streaming.Subsonic;
using Smoc.Streaming.YouTubeMusic;
using Terminal.Gui.App;

namespace Smoc.Streaming;

/// <summary>
/// Resolves streaming service credentials from credential stores, configuration, and legacy files.
/// </summary>
public static class ServiceCredentialResolver {
  /// <summary>
  /// Resolves Subsonic credentials from the provided <see cref="ICredentialStore"/> with fallback to configuration.
  /// </summary>
  /// <param name="credentialStore">The credential store to query.</param>
  /// <param name="config">Optional Subsonic configuration; defaults to <see cref="SubsonicConfig.Defaults"/>.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The resolved Subsonic credentials.</returns>
  /// <exception cref="InvalidOperationException">Thrown when host, username, or password is not configured, or when the credential store fails without a fallback.</exception>
  public static async Task<SubsonicCredentials> ResolveSubsonicAsync(
    ICredentialStore credentialStore,
    SubsonicConfig? config = null,
    CancellationToken cancellationToken = default
  ) {
    ArgumentNullException.ThrowIfNull(credentialStore);
    config ??= SubsonicConfig.Defaults;

    string host = config.ServerHost ?? throw new InvalidOperationException("Subsonic Server Host not configured");
    string username = config.Username ?? throw new InvalidOperationException("Subsonic Username not configured");

    (string? password, Exception? storeException) = await TryGetCredentialAsync(
        credentialStore,
        CredentialTargets.Subsonic,
        username,
        $"Subsonic password for user '{username}'",
        cancellationToken);

    password ??= config.Password;

    if (string.IsNullOrEmpty(password)) {
      if (storeException != null) {
        throw new InvalidOperationException(
            $"Subsonic Password could not be read from the credential store for user '{username}' ({storeException.Message}), and no fallback password is configured.",
            storeException);
      }

      throw new InvalidOperationException("Subsonic Password not configured");
    }

    return new SubsonicCredentials(config.ServerScheme, host, config.ServerPort, username, password, config.UseToken);
  }

  /// <summary>
  /// Resolves SoundCloud credentials from the provided <see cref="ICredentialStore"/> with fallback to configuration.
  /// </summary>
  /// <param name="credentialStore">The credential store to query.</param>
  /// <param name="config">Optional SoundCloud configuration; defaults to <see cref="SoundCloudConfig.Defaults"/>.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The resolved SoundCloud credentials.</returns>
  public static async Task<SoundCloudCredentials> ResolveSoundCloudAsync(
    ICredentialStore credentialStore,
    SoundCloudConfig? config = null,
    CancellationToken cancellationToken = default
  ) {
    ArgumentNullException.ThrowIfNull(credentialStore);
    config ??= SoundCloudConfig.Defaults;

    (string? authToken, _) = await TryGetCredentialAsync(
        credentialStore,
        CredentialTargets.SoundCloud,
        CredentialTargets.AccountAuthToken,
        "SoundCloud auth token",
        cancellationToken);

    return new SoundCloudCredentials(config.ClientId, authToken ?? config.AuthToken);
  }

  /// <summary>
  /// Resolves YouTube Music session credentials from the provided <see cref="ICredentialStore"/> with fallback to legacy files.
  /// </summary>
  /// <param name="credentialStore">The credential store to query.</param>
  /// <param name="cookiesPath">Optional path to cookies.txt; defaults to <see cref="SmocPaths.DefaultCookiesPath"/>.</param>
  /// <param name="tokensPath">Optional path to tokens.json; defaults to <see cref="SmocPaths.DefaultTokensPath"/>.</param>
  /// <param name="tokenGenerator">Optional token generator function for missing tokens.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The resolved session if cookies are present, or <c>null</c> for unauthenticated operation.</returns>
  public static async Task<YtmSession?> ResolveYouTubeMusicAsync(
    ICredentialStore credentialStore,
    string? cookiesPath = null,
    string? tokensPath = null,
    Func<List<Cookie>, Task<YtmTokens>>? tokenGenerator = null,
    CancellationToken cancellationToken = default
  ) {
    ArgumentNullException.ThrowIfNull(credentialStore);
    tokenGenerator ??= YtmStreamingClient.GenerateTokensAsync;

    List<Cookie>? storeCookies = await TryReadStoreCookiesAsync(credentialStore, cancellationToken);
    if (storeCookies != null) {
      return await ResolveStoreSessionAsync(credentialStore, storeCookies, tokenGenerator, cancellationToken);
    }

    string resolvedCookiesPath = cookiesPath ?? SmocPaths.DefaultCookiesPath;
    string resolvedTokensPath = tokensPath ?? SmocPaths.DefaultTokensPath;
    List<Cookie>? fileCookies = TryReadLegacyCookiesFile(resolvedCookiesPath);
    if (fileCookies != null) {
      return await ResolveLegacyFileSessionAsync(fileCookies, resolvedTokensPath, tokenGenerator, cancellationToken);
    }

    return null;
  }

  private static async Task<(string? Value, Exception? Error)> TryGetCredentialAsync(
    ICredentialStore credentialStore,
    string service,
    string account,
    string description,
    CancellationToken cancellationToken
  ) {
    try {
      string? value = await credentialStore.GetCredentialAsync(service, account, cancellationToken);
      return (value, null);
    } catch (Exception ex) when (ex is not OperationCanceledException) {
      Logging.Warning($"Failed to resolve {description} from credential store: {ex.Message}");
      return (null, ex);
    }
  }

  private static async Task<List<Cookie>?> TryReadStoreCookiesAsync(
    ICredentialStore credentialStore,
    CancellationToken cancellationToken
  ) {
    (string? cookiesRaw, _) = await TryGetCredentialAsync(
        credentialStore,
        CredentialTargets.YouTubeMusic,
        CredentialTargets.AccountCookies,
        "YouTube Music cookies",
        cancellationToken);

    if (string.IsNullOrWhiteSpace(cookiesRaw)) {
      return null;
    }

    List<Cookie> cookies = YtmStreamingClient.ParseCookies(cookiesRaw);
    return cookies.Count > 0 ? cookies : null;
  }

  private static List<Cookie>? TryReadLegacyCookiesFile(string cookiesPath) {
    if (!File.Exists(cookiesPath)) {
      return null;
    }

    try {
      List<Cookie> cookies = YtmStreamingClient.GetCookiesFromFile(cookiesPath);
      return cookies.Count > 0 ? cookies : null;
    } catch (Exception ex) when (ex is not OperationCanceledException) {
      Logging.Warning($"Failed to read legacy cookies file '{cookiesPath}': {ex.Message}");
      return null;
    }
  }

  private static async Task<YtmSession?> ResolveStoreSessionAsync(
    ICredentialStore credentialStore,
    List<Cookie> cookies,
    Func<List<Cookie>, Task<YtmTokens>> tokenGenerator,
    CancellationToken cancellationToken
  ) {
    (string? rawTokens, _) = await TryGetCredentialAsync(
        credentialStore,
        CredentialTargets.YouTubeMusic,
        CredentialTargets.AccountTokens,
        "YouTube Music tokens",
        cancellationToken);

    YtmTokens? tokens = TryDeserializeTokens(rawTokens, "credential store");
    if (tokens == null) {
      try {
        tokens = await tokenGenerator(cookies);
      } catch (Exception ex) when (ex is not OperationCanceledException) {
        Logging.Warning($"Failed to generate YouTube Music tokens from credential store: {ex.Message}");
        return null;
      }

      try {
        await credentialStore.SetCredentialAsync(
            CredentialTargets.YouTubeMusic,
            CredentialTargets.AccountTokens,
            JsonSerializer.Serialize(tokens),
            cancellationToken);
      } catch (Exception ex) when (ex is not OperationCanceledException) {
        Logging.Warning($"Failed to persist generated YouTube Music tokens to credential store: {ex.Message}");
      }
    }

    return new YtmSession(cookies, tokens);
  }

  private static async Task<YtmSession?> ResolveLegacyFileSessionAsync(
    List<Cookie> cookies,
    string tokensPath,
    Func<List<Cookie>, Task<YtmTokens>> tokenGenerator,
    CancellationToken cancellationToken
  ) {
    string? rawTokens = null;
    if (File.Exists(tokensPath)) {
      try {
        rawTokens = await File.ReadAllTextAsync(tokensPath, cancellationToken);
      } catch (IOException ex) {
        Logging.Warning($"Failed to read tokens from legacy file '{tokensPath}': {ex.Message}");
      }
    }

    string sourceDescription = $"legacy file '{tokensPath}'";
    YtmTokens? tokens = TryDeserializeTokens(rawTokens, sourceDescription);
    if (tokens == null) {
      try {
        tokens = await tokenGenerator(cookies);
      } catch (Exception ex) when (ex is not OperationCanceledException) {
        Logging.Warning($"Failed to generate YouTube Music tokens from {sourceDescription}: {ex.Message}");
        return null;
      }

      try {
        await File.WriteAllTextAsync(tokensPath, JsonSerializer.Serialize(tokens), cancellationToken);
        Logging.Information($"Generated YouTube Music tokens and saved to '{tokensPath}'. Run 'smoc auth migrate' to securely store credentials.");
      } catch (Exception ex) when (ex is not OperationCanceledException) {
        Logging.Warning($"Failed to persist generated YouTube Music tokens to {sourceDescription}: {ex.Message}");
      }
    }

    return new YtmSession(cookies, tokens);
  }

  private static YtmTokens? TryDeserializeTokens(string? rawTokens, string sourceDescription) {
    if (string.IsNullOrWhiteSpace(rawTokens)) {
      return null;
    }

    try {
      return JsonSerializer.Deserialize<YtmTokens>(rawTokens);
    } catch (JsonException ex) {
      Logging.Warning($"Failed to deserialize YouTube Music tokens from {sourceDescription}: {ex.Message}. Regenerating tokens.");
      return null;
    }
  }
}
