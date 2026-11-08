namespace Smoc.Cli.Auth.YouTubeMusic;

/// <summary>
/// Result of importing YouTube Music session cookies.
/// </summary>
/// <param name="Success">Whether the cookie import succeeded.</param>
/// <param name="Message">Descriptive status message or error details.</param>
/// <param name="CookieCount">Number of cookies parsed and stored.</param>
/// <param name="TokensGenerated">Whether PO and visitor tokens were successfully generated.</param>
/// <param name="SourceDeleted">Whether the source cookies file was removed.</param>
public record ImportCookiesResult(
  bool Success,
  string Message,
  int CookieCount,
  bool TokensGenerated,
  bool SourceDeleted
) {
  /// <summary>
  /// Creates a successful cookie import result.
  /// </summary>
  public static ImportCookiesResult Succeeded(string message, int cookieCount, bool tokensGenerated = true, bool sourceDeleted = false) =>
      new(true, message, cookieCount, tokensGenerated, sourceDeleted);

  /// <summary>
  /// Creates a failed cookie import result.
  /// </summary>
  public static ImportCookiesResult Failed(string message) =>
      new(false, message, 0, false, false);
}

