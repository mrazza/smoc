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

    string scheme = config.ServerScheme;
    string host = config.ServerHost ?? throw new InvalidOperationException("Subsonic Server Host not configured");
    int port = config.ServerPort;
    string username = config.Username ?? throw new InvalidOperationException("Subsonic Username not configured");
    bool useToken = config.UseToken;

    string? password = null;
    Exception? storeException = null;
    try {
      password = await credentialStore.GetCredentialAsync(CredentialTargets.Subsonic, username, cancellationToken);
    } catch (Exception ex) when (ex is not OperationCanceledException) {
      storeException = ex;
      Logging.Warning($"Failed to resolve Subsonic password from credential store for user '{username}': {ex.Message}");
    }

    password ??= config.Password;

    if (string.IsNullOrEmpty(password)) {
      if (storeException != null) {
        throw new InvalidOperationException(
            $"Subsonic Password could not be read from the credential store for user '{username}' ({storeException.Message}), and no fallback password is configured.",
            storeException);
      }

      throw new InvalidOperationException("Subsonic Password not configured");
    }

    return new SubsonicCredentials(scheme, host, port, username, password, useToken);
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

    string? authToken = null;
    try {
      authToken = await credentialStore.GetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
    } catch (Exception ex) when (ex is not OperationCanceledException) {
      Logging.Warning($"Failed to resolve SoundCloud auth token from credential store: {ex.Message}");
    }

    authToken ??= config.AuthToken;
    string? clientId = config.ClientId;

    return new SoundCloudCredentials(clientId, authToken);
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

    YtmSession? storeSession = await TryResolveFromStoreAsync(credentialStore, tokenGenerator, cancellationToken);
    if (storeSession != null) {
      return storeSession;
    }

    string resolvedCookiesPath = cookiesPath ?? SmocPaths.DefaultCookiesPath;
    string resolvedTokensPath = tokensPath ?? SmocPaths.DefaultTokensPath;
    return await TryResolveFromLegacyFilesAsync(resolvedCookiesPath, resolvedTokensPath, tokenGenerator, cancellationToken);
  }

  private static async Task<YtmSession?> TryResolveFromStoreAsync(
    ICredentialStore credentialStore,
    Func<List<Cookie>, Task<YtmTokens>> tokenGenerator,
    CancellationToken cancellationToken
  ) {
    string? cookiesRaw = null;
    string? tokensRaw = null;

    try {
      cookiesRaw = await credentialStore.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
    } catch (Exception ex) when (ex is not OperationCanceledException) {
      Logging.Warning($"Failed to read YouTube Music cookies from credential store: {ex.Message}");
    }

    if (string.IsNullOrWhiteSpace(cookiesRaw)) {
      return null;
    }

    List<Cookie> cookies = YtmStreamingClient.ParseCookies(cookiesRaw);
    if (cookies.Count == 0) {
      return null;
    }

    try {
      tokensRaw = await credentialStore.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);
    } catch (Exception ex) when (ex is not OperationCanceledException) {
      Logging.Warning($"Failed to read YouTube Music tokens from credential store: {ex.Message}");
    }

    YtmTokens? tokens = await GetOrGenerateTokensAsync(
      cookies,
      tokensRaw,
      "credential store",
      tokenGenerator,
      serialized => credentialStore.SetCredentialAsync(
          CredentialTargets.YouTubeMusic,
          CredentialTargets.AccountTokens,
          serialized,
          cancellationToken));

    return tokens != null ? new YtmSession(cookies, tokens) : null;
  }

  private static async Task<YtmSession?> TryResolveFromLegacyFilesAsync(
    string cookiesPath,
    string tokensPath,
    Func<List<Cookie>, Task<YtmTokens>> tokenGenerator,
    CancellationToken cancellationToken
  ) {
    if (!File.Exists(cookiesPath)) {
      return null;
    }

    List<Cookie> cookies;
    try {
      cookies = YtmStreamingClient.GetCookiesFromFile(cookiesPath);
    } catch (Exception ex) when (ex is not OperationCanceledException) {
      Logging.Warning($"Failed to read legacy cookies file '{cookiesPath}': {ex.Message}");
      return null;
    }

    if (cookies.Count == 0) {
      return null;
    }

    string? fileTokenContent = null;
    if (File.Exists(tokensPath)) {
      try {
        fileTokenContent = await File.ReadAllTextAsync(tokensPath, cancellationToken);
      } catch (IOException ex) {
        Logging.Warning($"Failed to read tokens from legacy file '{tokensPath}': {ex.Message}");
      }
    }

    YtmTokens? tokens = await GetOrGenerateTokensAsync(
      cookies,
      fileTokenContent,
      $"legacy file '{tokensPath}'",
      tokenGenerator,
      async serialized => {
        await File.WriteAllTextAsync(tokensPath, serialized, cancellationToken);
        Logging.Information($"Generated YouTube Music tokens and saved to '{tokensPath}'. Run 'smoc auth migrate' to securely store credentials.");
      });

    return tokens != null ? new YtmSession(cookies, tokens) : null;
  }

  private static async Task<YtmTokens?> GetOrGenerateTokensAsync(
    List<Cookie> cookies,
    string? rawTokens,
    string sourceDescription,
    Func<List<Cookie>, Task<YtmTokens>> tokenGenerator,
    Func<string, Task> persistTokensAsync
  ) {
    if (!string.IsNullOrWhiteSpace(rawTokens)) {
      try {
        YtmTokens? deserialized = JsonSerializer.Deserialize<YtmTokens>(rawTokens);
        if (deserialized != null) {
          return deserialized;
        }
      } catch (JsonException ex) {
        Logging.Warning($"Failed to deserialize YouTube Music tokens from {sourceDescription}: {ex.Message}. Regenerating tokens.");
      }
    }

    try {
      YtmTokens generated = await tokenGenerator(cookies);
      try {
        await persistTokensAsync(JsonSerializer.Serialize(generated));
      } catch (Exception ex) when (ex is not OperationCanceledException) {
        Logging.Warning($"Failed to persist generated YouTube Music tokens to {sourceDescription}: {ex.Message}");
      }
      return generated;
    } catch (Exception ex) when (ex is not OperationCanceledException) {
      Logging.Warning($"Failed to generate YouTube Music tokens from {sourceDescription}: {ex.Message}");
      return null;
    }
  }
}
