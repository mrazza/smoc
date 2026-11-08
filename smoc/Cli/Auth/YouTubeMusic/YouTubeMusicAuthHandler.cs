using System.Net;
using System.Text.Json;
using Smoc.Security;
using Smoc.Streaming.YouTubeMusic;

namespace Smoc.Cli.Auth.YouTubeMusic;

/// <summary>
/// Handles YouTube Music authentication operations, cookie import, session token generation, and credential migration.
/// </summary>
public class YouTubeMusicAuthHandler {
  private readonly ICredentialStore _credentialStore;
  private readonly string _cookiesPath;
  private readonly string _tokensPath;
  private readonly Func<List<Cookie>, Task<YtmTokens>> _tokenGenerator;

  /// <summary>
  /// Initializes a new instance of <see cref="YouTubeMusicAuthHandler"/>.
  /// </summary>
  /// <param name="credentialStore">The credential store implementation.</param>
  /// <param name="cookiesPath">Path to cookies.txt.</param>
  /// <param name="tokensPath">Path to tokens.json.</param>
  /// <param name="tokenGenerator">Function to generate YouTube Music tokens from cookies.</param>
  public YouTubeMusicAuthHandler(
    ICredentialStore credentialStore,
    string cookiesPath,
    string tokensPath,
    Func<List<Cookie>, Task<YtmTokens>> tokenGenerator
  ) {
    _credentialStore = credentialStore;
    _cookiesPath = cookiesPath;
    _tokensPath = tokensPath;
    _tokenGenerator = tokenGenerator;
  }

  /// <summary>
  /// Imports YouTube Music cookies from a file into the credential store and generates session tokens.
  /// </summary>
  /// <param name="cookieFilePath">Optional path to the cookies file (defaults to configured cookies path if null or whitespace).</param>
  /// <param name="deleteSource">Whether to delete the plaintext cookies file after successful import.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>An <see cref="ImportCookiesResult"/> containing the outcome details.</returns>
  public async Task<ImportCookiesResult> ImportCookiesAsync(
    string? cookieFilePath = null,
    bool deleteSource = false,
    CancellationToken cancellationToken = default
  ) {
    string resolvedPath = string.IsNullOrWhiteSpace(cookieFilePath) ? _cookiesPath : cookieFilePath;
    if (!File.Exists(resolvedPath)) {
      return ImportCookiesResult.Failed($"Cookies file not found at '{resolvedPath}'.");
    }

    try {
      string content = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
      if (string.IsNullOrWhiteSpace(content)) {
        return ImportCookiesResult.Failed($"Cookies file at '{resolvedPath}' is empty.");
      }

      List<Cookie> cookies = YtmStreamingClient.ParseCookies(content);
      if (cookies.Count == 0) {
        return ImportCookiesResult.Failed($"No valid cookies could be parsed from '{resolvedPath}'.");
      }

      await _credentialStore.SetCredentialAsync(
          CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, content.Trim(), cancellationToken);

      await GenerateAndStoreTokensAsync(cookies, cancellationToken);

      if (deleteSource) {
        try {
          File.Delete(resolvedPath);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
          return ImportCookiesResult.Succeeded(
              $"Successfully imported {cookies.Count} cookies and generated visitor/PO tokens, but failed to delete source file: {ex.Message}",
              cookies.Count,
              tokensGenerated: true,
              sourceDeleted: false);
        }
      }

      return ImportCookiesResult.Succeeded(
          $"Successfully imported {cookies.Count} cookies and generated visitor/PO tokens.",
          cookies.Count,
          tokensGenerated: true,
          sourceDeleted: deleteSource);
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      return ImportCookiesResult.Failed($"Failed to import cookies: {ex.Message}");
    }
  }

  /// <summary>
  /// Generates YouTube Music visitor and PO tokens from stored cookies and saves them to the credential store (or legacy tokens file).
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  public async Task GenerateTokensAsync(CancellationToken cancellationToken = default) {
    string? cookiesRaw = await _credentialStore.GetCredentialAsync(
        CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);

    if (!string.IsNullOrWhiteSpace(cookiesRaw)) {
      List<Cookie> storeCookies = YtmStreamingClient.ParseCookies(cookiesRaw);
      if (storeCookies.Count > 0) {
        await GenerateAndStoreTokensAsync(storeCookies, cancellationToken);
        return;
      }
    }

    if (File.Exists(_cookiesPath)) {
      List<Cookie> fileCookies = YtmStreamingClient.GetCookiesFromFile(_cookiesPath);
      if (fileCookies.Count > 0) {
        YtmTokens tokens = await _tokenGenerator(fileCookies);
        await File.WriteAllTextAsync(_tokensPath, JsonSerializer.Serialize(tokens), cancellationToken);
        return;
      }
    }

    throw new InvalidOperationException(
        "No YouTube Music cookies found in credential store or cookies.txt. Use 'smoc auth import-cookies <path>' to import cookies.");
  }

  /// <summary>
  /// Retrieves the current authentication and storage status for YouTube Music.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>A <see cref="ServiceAuthStatus"/> for YouTube Music.</returns>
  public async Task<ServiceAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default) {
    CredentialLookup cookieLookup = await AuthCredentialHelper.InspectCredentialAsync(_credentialStore, CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
    if (cookieLookup.Error != null) {
      return new ServiceAuthStatus("YouTube Music", AuthStatusState.Error, CredentialStorageKind.None, $"Keyring error: {cookieLookup.Error}");
    }

    CredentialLookup tokenLookup = await AuthCredentialHelper.InspectCredentialAsync(_credentialStore, CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);
    if (tokenLookup.Error != null) {
      return new ServiceAuthStatus("YouTube Music", AuthStatusState.Error, CredentialStorageKind.None, $"Keyring error: {tokenLookup.Error}");
    }

    if (!string.IsNullOrEmpty(cookieLookup.Value)) {
      bool hasTokens = !string.IsNullOrEmpty(tokenLookup.Value);
      string tokenDetail = hasTokens ? "Tokens present" : "Tokens missing; run 'smoc auth gentokens'";
      AuthStatusState status = hasTokens ? AuthStatusState.Authenticated : AuthStatusState.CookiesStored;
      return new ServiceAuthStatus("YouTube Music", status, cookieLookup.Storage, $"Cookies stored ({tokenDetail})");
    }

    if (File.Exists(_cookiesPath)) {
      return new ServiceAuthStatus("YouTube Music", AuthStatusState.ConfiguredInsecure, CredentialStorageKind.PlaintextFile, "Plaintext cookies.txt found (Run 'smoc auth migrate')");
    }

    return new ServiceAuthStatus("YouTube Music", AuthStatusState.NotConfigured, CredentialStorageKind.None, "No cookies stored");
  }

  /// <summary>
  /// Migrates plaintext YouTube Music cookies and tokens into the credential store.
  /// </summary>
  /// <param name="backupPlaintextFiles">Whether to back up plaintext files before deleting.</param>
  /// <param name="migratedItems">List tracking successfully migrated items.</param>
  /// <param name="cleanedFiles">List tracking cleaned file paths.</param>
  /// <param name="errors">List tracking migration errors.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  public async Task MigrateAsync(
    bool backupPlaintextFiles,
    List<string> migratedItems,
    List<string> cleanedFiles,
    List<string> errors,
    CancellationToken cancellationToken = default
  ) {
    if (!File.Exists(_cookiesPath)) {
      if (File.Exists(_tokensPath)) {
        AuthCredentialHelper.CleanPlaintextFile(_tokensPath, backupPlaintextFiles, cleanedFiles);
      }
      return;
    }

    try {
      string cookieContent = await File.ReadAllTextAsync(_cookiesPath, cancellationToken);
      if (!string.IsNullOrWhiteSpace(cookieContent)) {
        await _credentialStore.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cookieContent.Trim(), cancellationToken);
        migratedItems.Add("YouTube Music session cookies");

        if (File.Exists(_tokensPath)) {
          string tokenContent = await File.ReadAllTextAsync(_tokensPath, cancellationToken);
          if (!string.IsNullOrWhiteSpace(tokenContent)) {
            await _credentialStore.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, tokenContent.Trim(), cancellationToken);
            migratedItems.Add("YouTube Music visitor and PO tokens");
          }
        } else {
          List<Cookie> parsedCookies = YtmStreamingClient.ParseCookies(cookieContent);
          if (parsedCookies.Count > 0) {
            await GenerateAndStoreTokensAsync(parsedCookies, cancellationToken);
            migratedItems.Add("Generated YouTube Music visitor and PO tokens");
          }
        }

        AuthCredentialHelper.CleanPlaintextFile(_cookiesPath, backupPlaintextFiles, cleanedFiles);
        AuthCredentialHelper.CleanPlaintextFile(_tokensPath, backupPlaintextFiles, cleanedFiles);
      }
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      errors.Add($"Failed to migrate YouTube Music cookies/tokens: {ex.Message}");
    }
  }

  /// <summary>
  /// Logs out of YouTube Music by deleting stored credentials.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns><c>true</c> if credentials were removed; otherwise, <c>false</c>.</returns>
  public async Task<bool> LogoutAsync(CancellationToken cancellationToken = default) {
    bool removedCookies = await AuthCredentialHelper.DeleteCredentialIfPresentAsync(_credentialStore, CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
    bool removedTokens = await AuthCredentialHelper.DeleteCredentialIfPresentAsync(_credentialStore, CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);
    return removedCookies || removedTokens;
  }

  private async Task GenerateAndStoreTokensAsync(List<Cookie> cookies, CancellationToken cancellationToken) {
    YtmTokens tokens = await _tokenGenerator(cookies);
    await _credentialStore.SetCredentialAsync(
        CredentialTargets.YouTubeMusic,
        CredentialTargets.AccountTokens,
        JsonSerializer.Serialize(tokens),
        cancellationToken);
  }
}

