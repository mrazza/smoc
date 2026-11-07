namespace Smoc.Streaming.Subsonic;

/// <summary>
/// Connection and authentication credentials for Subsonic-compatible servers.
/// </summary>
/// <param name="Scheme">The URI scheme (http or https).</param>
/// <param name="Host">The server hostname or IP address.</param>
/// <param name="Port">The server port.</param>
/// <param name="Username">The Subsonic username.</param>
/// <param name="Password">The Subsonic password or authentication secret.</param>
/// <param name="UseToken">Whether to use token-based authentication (salt + md5) instead of plaintext password.</param>
public record SubsonicCredentials(
  string Scheme,
  string Host,
  int Port,
  string Username,
  string Password,
  bool UseToken
);
