using System.Text.Json;

namespace Smoc.Streaming.Subsonic;

/// <summary>
/// Provides shared API URL construction and response validation for Subsonic-compatible servers.
/// </summary>
public static class SubsonicApi {
  /// <summary>
  /// The client identifier string sent in Subsonic requests.
  /// </summary>
  public const string ClientName = "smoc";

  /// <summary>
  /// The Subsonic REST protocol version supported.
  /// </summary>
  public const string ProtocolVersion = "1.16.1";

  /// <summary>
  /// Builds a fully qualified Subsonic REST endpoint URI.
  /// </summary>
  /// <param name="scheme">URL scheme (http or https).</param>
  /// <param name="host">Server hostname or IP address.</param>
  /// <param name="port">Server port number.</param>
  /// <param name="method">Subsonic REST method name (e.g. ping.view, search3.view).</param>
  /// <param name="username">Subsonic username.</param>
  /// <param name="password">Subsonic password or plaintext secret.</param>
  /// <param name="useToken">Whether to use MD5 token/salt authentication.</param>
  /// <param name="parameters">Optional additional query parameters.</param>
  /// <returns>The constructed <see cref="Uri"/>.</returns>
  public static Uri BuildUri(
    string scheme,
    string host,
    int port,
    string method,
    string username,
    string password,
    bool useToken,
    IEnumerable<KeyValuePair<string, string>>? parameters = null
  ) {
    ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
    ArgumentException.ThrowIfNullOrWhiteSpace(host);
    ArgumentException.ThrowIfNullOrWhiteSpace(username);

    List<string> queryParams = [
      $"u={Uri.EscapeDataString(username)}",
      $"v={ProtocolVersion}",
      $"c={ClientName}",
      "f=json"
    ];

    if (useToken) {
      var (token, salt) = SubsonicAuthentication.GenerateToken(password);
      queryParams.Add($"t={token}");
      queryParams.Add($"s={salt}");
    } else {
      queryParams.Add($"p={Uri.EscapeDataString(password)}");
    }

    if (parameters != null) {
      foreach (var param in parameters) {
        queryParams.Add($"{param.Key}={Uri.EscapeDataString(param.Value)}");
      }
    }

    string path = method.StartsWith("rest/") ? method : $"rest/{method}";
    return new UriBuilder(scheme, host, port, path) {
      Query = string.Join("&", queryParams)
    }.Uri;
  }

  /// <summary>
  /// Validates a Subsonic JSON response element, throwing if the server reported an error.
  /// </summary>
  /// <param name="rootElement">The root JSON element containing subsonic-response.</param>
  /// <returns>The unwrapped subsonic-response element.</returns>
  /// <exception cref="InvalidOperationException">Thrown if subsonic-response is missing or status is failed.</exception>
  public static JsonElement ValidateResponse(JsonElement rootElement) {
    if (!rootElement.TryGetProperty("subsonic-response", out JsonElement responseElement)) {
      throw new Exception("Missing subsonic-response");
    }

    if (responseElement.TryGetProperty("status", out JsonElement status) && status.GetString() == "failed") {
      string errorMessage = "Unknown Subsonic error";
      int errorCode = 0;
      if (responseElement.TryGetProperty("error", out JsonElement error)) {
        if (error.TryGetProperty("code", out JsonElement code)) {
          errorCode = code.GetInt32();
        }
        if (error.TryGetProperty("message", out JsonElement msg)) {
          errorMessage = msg.GetString() ?? errorMessage;
        }
      }
      throw new Exception($"Subsonic API error {errorCode}: {errorMessage}");
    }

    return responseElement;
  }
}
