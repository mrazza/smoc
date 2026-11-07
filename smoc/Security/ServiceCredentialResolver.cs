using System.Net;
using System.Text.Json;
using Smoc.Configuration;
using Smoc.Streaming.SoundCloud;
using Smoc.Streaming.Subsonic;
using Smoc.Streaming.YouTubeMusic;
using Terminal.Gui.App;

namespace Smoc.Security;

/// <summary>
/// Resolves streaming service credentials from credential stores, configuration, and legacy files.
/// </summary>
public static class ServiceCredentialResolver {
  /// <summary>
  /// Resolves Subsonic credentials from the provided <see cref="ICredentialStore"/> with fallback to configuration.
  /// </summary>
  /// <param name="credentialStore">The credential store to query.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The resolved Subsonic credentials.</returns>
  /// <exception cref="InvalidOperationException">Thrown when host, username, or password is not configured.</exception>
  public static async Task<SubsonicCredentials> ResolveSubsonicAsync(
    ICredentialStore credentialStore,
    CancellationToken cancellationToken = default
  ) {
    ArgumentNullException.ThrowIfNull(credentialStore);

    string scheme = SubsonicConfig.Defaults.ServerScheme;
    string host = SubsonicConfig.Defaults.ServerHost ?? throw new InvalidOperationException("Subsonic Server Host not configured");
    int port = SubsonicConfig.Defaults.ServerPort;
    string username = SubsonicConfig.Defaults.Username ?? throw new InvalidOperationException("Subsonic Username not configured");
    bool useToken = SubsonicConfig.Defaults.UseToken;

    string? password = null;
    try {
      password = await credentialStore.GetCredentialAsync(CredentialTargets.Subsonic, username, cancellationToken);
    } catch (Exception ex) {
      Logging.Warning($"Failed to resolve Subsonic password from credential store for user '{username}': {ex.Message}");
    }

    password ??= SubsonicConfig.Defaults.Password;

    if (string.IsNullOrEmpty(password)) {
      throw new InvalidOperationException("Subsonic Password not configured");
    }

    return new SubsonicCredentials(scheme, host, port, username, password, useToken);
  }

  /// <summary>
  /// Resolves SoundCloud credentials from the provided <see cref="ICredentialStore"/> with fallback to configuration.
  /// </summary>
  /// <param name="credentialStore">The credential store to query.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The resolved SoundCloud credentials.</returns>
  public static async Task<SoundCloudCredentials> ResolveSoundCloudAsync(
    ICredentialStore credentialStore,
    CancellationToken cancellationToken = default
  ) {
    ArgumentNullException.ThrowIfNull(credentialStore);

    string? authToken = null;
    string? clientId = null;

    try {
      authToken = await credentialStore.GetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
    } catch (Exception ex) {
      Logging.Warning($"Failed to resolve SoundCloud auth token from credential store: {ex.Message}");
    }

    try {
      clientId = await credentialStore.GetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, cancellationToken);
    } catch (Exception ex) {
      Logging.Warning($"Failed to resolve SoundCloud client ID from credential store: {ex.Message}");
    }

    authToken ??= SoundCloudConfig.Defaults.AuthToken;
    clientId ??= SoundCloudConfig.Defaults.ClientId;

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

    // 1. Attempt resolution from ICredentialStore
    string? cookiesRaw = null;
    string? tokensRaw = null;

    try {
      cookiesRaw = await credentialStore.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
    } catch (Exception ex) {
      Logging.Warning($"Failed to read YouTube Music cookies from credential store: {ex.Message}");
    }

    try {
      tokensRaw = await credentialStore.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);
    } catch (Exception ex) {
      Logging.Warning($"Failed to read YouTube Music tokens from credential store: {ex.Message}");
    }

    if (!string.IsNullOrWhiteSpace(cookiesRaw)) {
      List<Cookie> cookies = YtmStreamingClient.ParseCookies(cookiesRaw);
      if (cookies.Count > 0) {
        YtmTokens? tokens = null;
        if (!string.IsNullOrWhiteSpace(tokensRaw)) {
          try {
            tokens = JsonSerializer.Deserialize<YtmTokens>(tokensRaw);
          } catch (JsonException ex) {
            Logging.Warning($"Failed to deserialize YouTube Music tokens from credential store: {ex.Message}. Regenerating tokens.");
          }
        }

        if (tokens == null) {
          try {
            tokenGenerator ??= YtmStreamingClient.GenerateTokensAsync;
            tokens = await tokenGenerator(cookies);
            try {
              await credentialStore.SetCredentialAsync(
                CredentialTargets.YouTubeMusic,
                CredentialTargets.AccountTokens,
                JsonSerializer.Serialize(tokens),
                cancellationToken
              );
            } catch (Exception ex) {
              Logging.Warning($"Failed to persist regenerated YouTube Music tokens to credential store: {ex.Message}");
            }
          } catch (Exception ex) {
            Logging.Warning($"Failed to generate YouTube Music tokens from store cookies: {ex.Message}");
          }
        }

        if (tokens != null) {
          return new YtmSession(cookies, tokens);
        }
      }
    }

    // 2. Fallback to legacy plaintext files
    string resolvedCookiesPath = cookiesPath ?? SmocPaths.DefaultCookiesPath;
    string resolvedTokensPath = tokensPath ?? SmocPaths.DefaultTokensPath;

    if (File.Exists(resolvedCookiesPath)) {
      try {
        List<Cookie> cookies = YtmStreamingClient.GetCookiesFromFile(resolvedCookiesPath);
        if (cookies.Count > 0) {
          YtmTokens? tokens = null;
          if (File.Exists(resolvedTokensPath)) {
            try {
              string fileTokenContent = await File.ReadAllTextAsync(resolvedTokensPath, cancellationToken);
              tokens = JsonSerializer.Deserialize<YtmTokens>(fileTokenContent);
            } catch (JsonException ex) {
              Logging.Warning($"Failed to deserialize tokens from legacy file '{resolvedTokensPath}': {ex.Message}. Regenerating tokens.");
            } catch (IOException ex) {
              Logging.Warning($"Failed to read tokens from legacy file '{resolvedTokensPath}': {ex.Message}");
            }
          }

          if (tokens == null) {
            try {
              tokenGenerator ??= YtmStreamingClient.GenerateTokensAsync;
              tokens = await tokenGenerator(cookies);
              try {
                await File.WriteAllTextAsync(resolvedTokensPath, JsonSerializer.Serialize(tokens), cancellationToken);
                Logging.Information($"Generated YouTube Music tokens and saved to '{resolvedTokensPath}'. Run 'smoc auth migrate' to securely store credentials.");
              } catch (Exception ex) {
                Logging.Warning($"Failed to save generated tokens to legacy path '{resolvedTokensPath}': {ex.Message}");
              }
            } catch (Exception ex) {
              Logging.Warning($"Failed to generate YouTube Music tokens from legacy cookies file: {ex.Message}");
            }
          }

          if (tokens != null) {
            return new YtmSession(cookies, tokens);
          }
        }
      } catch (Exception ex) {
        Logging.Warning($"Failed to read legacy cookies file '{resolvedCookiesPath}': {ex.Message}");
      }
    }

    return null;
  }
}
