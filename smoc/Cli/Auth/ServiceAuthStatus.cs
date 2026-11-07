namespace Smoc.Cli.Auth;

/// <summary>
/// Represents the authentication and storage status of a streaming service.
/// </summary>
/// <param name="Service">The user-friendly name of the streaming service.</param>
/// <param name="Status">The current authentication status (e.g. Authenticated, Configured, Not Configured).</param>
/// <param name="Storage">The credential storage mechanism (e.g. Keyring, Environment, Plaintext Config, None).</param>
/// <param name="Details">Additional context such as username, host, or cookie/token validity.</param>
public record ServiceAuthStatus(
  string Service,
  string Status,
  string Storage,
  string Details
);
