using System.Web;

namespace Smoc.Streaming.YouTubeMusic;

/// <summary>
/// Parses YouTube Music URLs.
/// </summary>
public static class YtmUrlParser {
  /// <summary>
  /// Parses the specified YouTube Music URL and returns the entity type and ID.
  /// </summary>
  /// <remarks>
  /// Examples:
  /// <para>https://music.youtube.com/watch?v=t89X6bRKa5Q&amp;si=gg-K-XehG6V1OuUm</para>
  /// <para>https://music.youtube.com/playlist?list=PLlGj3K-4-Q534695x8-h5938222681412&amp;si=gg-K-XehG6V1OuUm</para>
  /// </remarks>
  /// <param name="url">The URL to parse.</param>
  /// <returns>The entity type and ID.</returns>
  public static (Type EntityType, string Id) ParseUrl(string url) {
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != "music.youtube.com") {
      throw new ArgumentException($"Invalid URL. Must be a YouTube Music URL. {url}", nameof(url));
    }

    switch (uri.AbsolutePath) {
      case "/watch": {
        var query = HttpUtility.ParseQueryString(uri.Query);
        var videoId = query["v"];
        if (string.IsNullOrWhiteSpace(videoId)) {
          throw new ArgumentException("Invalid URL.", nameof(url));
        }
        return (typeof(Song), videoId);
      }
      case "/playlist": {
        var query = HttpUtility.ParseQueryString(uri.Query);
        var playlistId = query["list"];
        if (string.IsNullOrWhiteSpace(playlistId)) {
          throw new ArgumentException("Invalid URL.", nameof(url));
        }
        return (typeof(Playlist), playlistId);
      }
      default: {
        throw new ArgumentException("Invalid URL.", nameof(url));
      }
    }
  }
}
