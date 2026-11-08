namespace Smoc.Cli.Auth;

/// <summary>
/// Service interface for streaming authentication workflows, credential migration, and status reporting.
/// </summary>
public interface IAuthService {
  /// <summary>
  /// Logs into Subsonic by storing user credentials in the credential store and saving server parameters to config.
  /// </summary>
  /// <param name="host">The Subsonic server hostname or IP.</param>
  /// <param name="port">The Subsonic server port number.</param>
  /// <param name="scheme">The URL scheme (e.g. http or https).</param>
  /// <param name="username">The Subsonic username.</param>
  /// <param name="password">The Subsonic password.</param>
  /// <param name="useToken">Whether to use token-based authentication.</param>
  /// <param name="verifyConnection">Whether to verify connection to the Subsonic server before saving.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  Task LoginSubsonicAsync(
    string host,
    int port,
    string scheme,
    string username,
    string password,
    bool useToken,
    bool verifyConnection = true,
    CancellationToken cancellationToken = default
  );

  /// <summary>
  /// Logs into SoundCloud by storing the authentication token in the credential store and optional client ID in config.
  /// </summary>
  /// <param name="clientId">The SoundCloud client ID.</param>
  /// <param name="authToken">The SoundCloud OAuth authentication token.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  Task LoginSoundCloudAsync(
    string clientId,
    string authToken,
    CancellationToken cancellationToken = default
  );

  /// <summary>
  /// Imports YouTube Music cookies from a file into the credential store and generates session tokens.
  /// </summary>
  /// <param name="cookieFilePath">Optional path to the cookies file (defaults to configured cookies path if null or whitespace).</param>
  /// <param name="deleteSource">Whether to delete the plaintext cookies file after successful import.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>An <see cref="ImportCookiesResult"/> containing the outcome details.</returns>
  Task<ImportCookiesResult> ImportCookiesAsync(
    string? cookieFilePath = null,
    bool deleteSource = false,
    CancellationToken cancellationToken = default
  );

  /// <summary>
  /// Retrieves the current authentication and configuration status across all supported streaming services.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>A list of <see cref="ServiceAuthStatus"/> instances.</returns>
  Task<IReadOnlyList<ServiceAuthStatus>> GetStatusAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Logs out of the specified service by deleting its credentials from the credential store.
  /// </summary>
  /// <param name="serviceName">The name of the service to log out of (subsonic, soundcloud, youtube, or all).</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns><c>true</c> if credentials were removed; otherwise <c>false</c>.</returns>
  Task<bool> LogoutAsync(string serviceName, CancellationToken cancellationToken = default);

  /// <summary>
  /// Migrates plaintext credentials from configuration files and cookies into the credential store, sanitizing plaintext files.
  /// </summary>
  /// <param name="backupPlaintextFiles">Whether to back up plaintext files (.bak) before removal.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>A <see cref="MigrationResult"/> detailing the migrated items and cleaned files.</returns>
  Task<MigrationResult> MigrateAsync(bool backupPlaintextFiles = false, CancellationToken cancellationToken = default);
}
