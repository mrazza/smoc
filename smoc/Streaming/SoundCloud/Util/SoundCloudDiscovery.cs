using System.Text.RegularExpressions;

namespace Smoc.Streaming.SoundCloud.Util;

/// <summary>
/// Provides methods for discovering SoundCloud client configuration from the web page.
/// </summary>
public static class SoundCloudDiscovery {
  private static readonly Regex _scriptRegex = new Regex("<script[^>]+src=\"([^\"]+)\"", RegexOptions.IgnoreCase);
  private static readonly Regex _clientIdRegex = new Regex("client_id:\"([a-zA-Z0-9]{32})\"");

  /// <summary>
  /// Extracts script URLs from the provided HTML.
  /// </summary>
  /// <param name="html">The HTML to extract URLs from.</param>
  /// <returns>A collection of script URLs.</returns>
  public static IEnumerable<string> ExtractScriptUrls(string html) {
    return _scriptRegex.Matches(html).Select(m => m.Groups[1].Value);
  }

  /// <summary>
  /// Extracts the SoundCloud client ID from the provided script content.
  /// </summary>
  /// <param name="scriptContent">The script content to search.</param>
  /// <returns>The client ID if found; otherwise, <c>null</c>.</returns>
  public static string? ExtractClientId(string scriptContent) {
    var match = _clientIdRegex.Match(scriptContent);
    return match.Success ? match.Groups[1].Value : null;
  }
}