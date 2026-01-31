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
  /// <para>https://music.youtube.com/watch?v=t89X6bRKa5Q&si=gg-K-XehG6V1OuUm</para>
  /// <para>https://music.youtube.com/playlist?list=PLlGj3K-4-Q534695x8-h5938222681412&si=gg-K-XehG6V1OuUm</para>
  /// </remarks>
  /// <param name="url">The URL to parse.</param>
  /// <returns>The entity type and ID.</returns>
  public static (Type EntityType, string Id) ParseUrl(string url) {
    var uri = new Uri(url);
    if (uri.Host != "music.youtube.com")
      throw new ArgumentException($"Invalid URL. Must be a YouTube Music URL. {url}", nameof(url));

    switch (uri.AbsolutePath) {
      case "/watch": {
          var query = HttpUtility.ParseQueryString(uri.Query);
          return (typeof(Song), query["v"] ?? throw new ArgumentException("Invalid URL.", nameof(url)));
        }
      case "/playlist": {
          var query = HttpUtility.ParseQueryString(uri.Query);
          return (typeof(Playlist), query["list"] ?? throw new ArgumentException("Invalid URL.", nameof(url)));
        }
      default:
        throw new ArgumentException("Invalid URL.", nameof(url));
    }
  }
}