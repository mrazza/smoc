using System.Text.RegularExpressions;

namespace Smoc.Streaming.Tidal;

/// <summary>
/// Parser for Tidal URLs identifying tracks, albums, and playlists.
/// </summary>
public static class TidalUrlParser {
  private static readonly Regex UrlRegex = new(@"/(track|album|playlist)/([a-zA-Z0-9\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

  /// <summary>
  /// Parses the specified Tidal URL and returns the entity type and ID if valid.
  /// </summary>
  /// <param name="url">The Tidal URL to parse.</param>
  /// <param name="type">The parsed entity type ("track", "album", or "playlist").</param>
  /// <param name="id">The parsed ID or UUID.</param>
  /// <returns>True if parsing succeeded; otherwise false.</returns>
  public static bool TryParseUrl(string url, out string type, out string id) {
    type = string.Empty;
    id = string.Empty;

    if (string.IsNullOrWhiteSpace(url)) {
      return false;
    }

    var match = UrlRegex.Match(url);
    if (!match.Success) {
      return false;
    }

    type = match.Groups[1].Value.ToLowerInvariant();
    id = match.Groups[2].Value;
    return true;
  }
}
