using Smoc.Streaming;
using Smoc.Streaming.Spotify.Util;
using Xunit;

namespace smoc.Tests.Streaming.Spotify;

/// <summary>
/// Unit tests for <see cref="SpotifyUrlParser"/>.
/// </summary>
public class SpotifyUrlParserTest {
  /// <summary>
  /// Verifies parsing of standard Spotify track URLs with or without query strings and international prefixes.
  /// </summary>
  /// <param name="url">The track URL to parse.</param>
  /// <param name="expectedId">The expected track ID.</param>
  [Theory]
  [InlineData("https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT", "4cOdK2wGLETKBW3PvgPWqT")]
  [InlineData("https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT?si=abc12345", "4cOdK2wGLETKBW3PvgPWqT")]
  [InlineData("http://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT", "4cOdK2wGLETKBW3PvgPWqT")]
  [InlineData("open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT", "4cOdK2wGLETKBW3PvgPWqT")]
  [InlineData("https://open.spotify.com/intl-de/track/4cOdK2wGLETKBW3PvgPWqT", "4cOdK2wGLETKBW3PvgPWqT")]
  [InlineData("https://open.spotify.com/intl-ja/track/4cOdK2wGLETKBW3PvgPWqT?si=xyz", "4cOdK2wGLETKBW3PvgPWqT")]
  public void ParseUrl_WithTrackUrl_ReturnsSongTypeAndId(string url, string expectedId) {
    var (entityType, id) = SpotifyUrlParser.ParseUrl(url);

    Assert.Equal(typeof(Song), entityType);
    Assert.Equal(expectedId, id);
  }

  /// <summary>
  /// Verifies parsing of standard Spotify album URLs.
  /// </summary>
  /// <param name="url">The album URL to parse.</param>
  /// <param name="expectedId">The expected album ID.</param>
  [Theory]
  [InlineData("https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy", "4aawyAB9vmqN3uQ7FjRGTy")]
  [InlineData("https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy?si=share123", "4aawyAB9vmqN3uQ7FjRGTy")]
  [InlineData("open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy", "4aawyAB9vmqN3uQ7FjRGTy")]
  [InlineData("https://open.spotify.com/intl-fr/album/4aawyAB9vmqN3uQ7FjRGTy", "4aawyAB9vmqN3uQ7FjRGTy")]
  public void ParseUrl_WithAlbumUrl_ReturnsAlbumTypeAndId(string url, string expectedId) {
    var (entityType, id) = SpotifyUrlParser.ParseUrl(url);

    Assert.Equal(typeof(Album), entityType);
    Assert.Equal(expectedId, id);
  }

  /// <summary>
  /// Verifies parsing of standard Spotify playlist URLs.
  /// </summary>
  /// <param name="url">The playlist URL to parse.</param>
  /// <param name="expectedId">The expected playlist ID.</param>
  [Theory]
  [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
  [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M?si=play123", "37i9dQZF1DXcBWIGoYBM5M")]
  [InlineData("open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
  [InlineData("https://open.spotify.com/intl-es/playlist/37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
  public void ParseUrl_WithPlaylistUrl_ReturnsPlaylistTypeAndId(string url, string expectedId) {
    var (entityType, id) = SpotifyUrlParser.ParseUrl(url);

    Assert.Equal(typeof(Playlist), entityType);
    Assert.Equal(expectedId, id);
  }

  /// <summary>
  /// Verifies parsing of standard Spotify artist URLs.
  /// </summary>
  /// <param name="url">The artist URL to parse.</param>
  /// <param name="expectedId">The expected artist ID.</param>
  [Theory]
  [InlineData("https://open.spotify.com/artist/0OdUWJ0sBjDrqHygGUXeCF", "0OdUWJ0sBjDrqHygGUXeCF")]
  [InlineData("https://open.spotify.com/artist/0OdUWJ0sBjDrqHygGUXeCF?si=art123", "0OdUWJ0sBjDrqHygGUXeCF")]
  public void ParseUrl_WithArtistUrl_ReturnsArtistTypeAndId(string url, string expectedId) {
    var (entityType, id) = SpotifyUrlParser.ParseUrl(url);

    Assert.Equal(typeof(Artist), entityType);
    Assert.Equal(expectedId, id);
  }

  /// <summary>
  /// Verifies parsing of spotify:... URI formats.
  /// </summary>
  /// <param name="uri">The URI to parse.</param>
  /// <param name="expectedType">The expected entity type.</param>
  /// <param name="expectedId">The expected entity ID.</param>
  [Theory]
  [InlineData("spotify:track:4cOdK2wGLETKBW3PvgPWqT", typeof(Song), "4cOdK2wGLETKBW3PvgPWqT")]
  [InlineData("spotify:album:4aawyAB9vmqN3uQ7FjRGTy", typeof(Album), "4aawyAB9vmqN3uQ7FjRGTy")]
  [InlineData("spotify:playlist:37i9dQZF1DXcBWIGoYBM5M", typeof(Playlist), "37i9dQZF1DXcBWIGoYBM5M")]
  [InlineData("spotify:artist:0OdUWJ0sBjDrqHygGUXeCF", typeof(Artist), "0OdUWJ0sBjDrqHygGUXeCF")]
  [InlineData("spotify:track:4cOdK2wGLETKBW3PvgPWqT?si=abc", typeof(Song), "4cOdK2wGLETKBW3PvgPWqT")]
  public void ParseUrl_WithSpotifyUri_ReturnsCorrectTypeAndId(string uri, Type expectedType, string expectedId) {
    var (entityType, id) = SpotifyUrlParser.ParseUrl(uri);

    Assert.Equal(expectedType, entityType);
    Assert.Equal(expectedId, id);
  }

  /// <summary>
  /// Verifies that invalid, unsupported, or non-Spotify URLs and URIs throw <see cref="ArgumentException"/>.
  /// </summary>
  /// <param name="invalidUrl">The invalid URL or URI.</param>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("https://music.youtube.com/watch?v=12345")]
  [InlineData("https://soundcloud.com/artist/track")]
  [InlineData("spotify:user:someone")]
  [InlineData("spotify:invalid")]
  [InlineData("spotify:")]
  [InlineData("https://open.spotify.com/unsupported/12345")]
  [InlineData("not-a-url")]
  public void ParseUrl_WithInvalidOrUnsupportedUrls_ThrowsArgumentException(string? invalidUrl) {
    Assert.Throws<ArgumentException>(() => SpotifyUrlParser.ParseUrl(invalidUrl!));
  }
}
