using System.Web;

namespace Smoc.Streaming.Spotify.Util;

/// <summary>
/// Parses Spotify URLs and URIs into entity types and identifiers.
/// </summary>
public static class SpotifyUrlParser {
  /// <summary>
  /// Parses the specified Spotify URL or URI and returns the entity type and ID.
  /// </summary>
  /// <remarks>
  /// Examples:
  /// <para>https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT</para>
  /// <para>https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy</para>
  /// <para>https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M</para>
  /// <para>spotify:track:4cOdK2wGLETKBW3PvgPWqT</para>
  /// <para>spotify:album:4aawyAB9vmqN3uQ7FjRGTy</para>
  /// <para>spotify:playlist:37i9dQZF1DXcBWIGoYBM5M</para>
  /// </remarks>
  /// <param name="url">The URL or URI to parse.</param>
  /// <returns>A tuple containing the entity type and the entity ID.</returns>
  /// <exception cref="ArgumentException">Thrown when the URL/URI is invalid or unsupported.</exception>
  public static (Type EntityType, string Id) ParseUrl(string url) {
    if (string.IsNullOrWhiteSpace(url)) {
      throw new ArgumentException("URL cannot be null or empty.", nameof(url));
    }

    var trimmed = url.Trim();

    // Handle spotify:type:id URIs
    if (trimmed.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase)) {
      var parts = trimmed.Split(':');
      if (parts.Length >= 3) {
        var uriType = parts[1].ToLowerInvariant();
        var uriId = parts[2].Split('?')[0].Trim();
        if (!string.IsNullOrEmpty(uriId)) {
          return uriType switch {
            "track" => (typeof(Song), uriId),
            "album" => (typeof(Album), uriId),
            "playlist" => (typeof(Playlist), uriId),
            "artist" => (typeof(Artist), uriId),
            _ => throw new ArgumentException($"Unsupported Spotify URI entity type: {uriType}", nameof(url))
          };
        }
      }
      throw new ArgumentException($"Invalid Spotify URI format: {url}", nameof(url));
    }

    // Ensure scheme for HTTP/HTTPS URLs
    if (!trimmed.Contains("://")) {
      trimmed = "https://" + trimmed;
    }

    if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) {
      throw new ArgumentException($"Invalid URL format: {url}", nameof(url));
    }

    if (!uri.Host.Equals("open.spotify.com", StringComparison.OrdinalIgnoreCase) &&
        !uri.Host.Equals("spotify.com", StringComparison.OrdinalIgnoreCase) &&
        !uri.Host.EndsWith(".spotify.com", StringComparison.OrdinalIgnoreCase)) {
      throw new ArgumentException($"Invalid URL. Must be a Spotify URL: {url}", nameof(url));
    }

    var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    for (var i = 0; i < segments.Length - 1; i++) {
      var segment = segments[i].ToLowerInvariant();
      if (segment is "track" or "album" or "playlist" or "artist") {
        var id = segments[i + 1].Split('?')[0].Trim();
        if (!string.IsNullOrEmpty(id)) {
          return segment switch {
            "track" => (typeof(Song), id),
            "album" => (typeof(Album), id),
            "playlist" => (typeof(Playlist), id),
            "artist" => (typeof(Artist), id),
            _ => throw new ArgumentException($"Unsupported Spotify entity type: {segment}", nameof(url))
          };
        }
      }
    }

    throw new ArgumentException($"Invalid or unsupported Spotify URL: {url}", nameof(url));
  }
}
