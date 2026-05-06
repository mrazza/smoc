using Smoc.Streaming;
using SpotifyAPI.Web;

namespace Smoc.Streaming.Spotify.Util;

/// <summary>
/// Provides utility methods for mapping Spotify models to SMoC streaming models.
/// </summary>
public static class SpotifyMapper {
  /// <summary>
  /// Maps a Spotify <see cref="SimpleArtist"/> to an <see cref="Artist"/>.
  /// </summary>
  /// <param name="artist">The Spotify simple artist to map.</param>
  /// <returns>The mapped SMoC <see cref="Artist"/>.</returns>
  public static Artist MapArtist(SimpleArtist? artist) {
    if (artist == null) {
      return new Artist(string.Empty, "Unknown Artist");
    }
    return new Artist(artist.Id ?? string.Empty, string.IsNullOrWhiteSpace(artist.Name) ? "Unknown Artist" : artist.Name);
  }

  /// <summary>
  /// Maps a Spotify <see cref="FullArtist"/> to an <see cref="Artist"/>.
  /// </summary>
  /// <param name="artist">The Spotify full artist to map.</param>
  /// <returns>The mapped SMoC <see cref="Artist"/>.</returns>
  public static Artist MapArtist(FullArtist? artist) {
    if (artist == null) {
      return new Artist(string.Empty, "Unknown Artist");
    }
    return new Artist(artist.Id ?? string.Empty, string.IsNullOrWhiteSpace(artist.Name) ? "Unknown Artist" : artist.Name);
  }

  /// <summary>
  /// Maps a Spotify <see cref="SimpleAlbum"/> to an <see cref="Album"/>.
  /// </summary>
  /// <param name="album">The Spotify simple album to map.</param>
  /// <param name="artist">Optional pre-mapped artist. If not provided, the first artist in the album is used.</param>
  /// <returns>The mapped SMoC <see cref="Album"/>.</returns>
  public static Album MapSimpleAlbumToAlbum(SimpleAlbum album, Artist? artist = null) {
    var albumArtist = artist ?? (album.Artists?.Count > 0 ? MapArtist(album.Artists[0]) : new Artist(string.Empty, "Unknown Artist"));
    var covers = MapImagesToCovers(album.Images);
    var releaseYear = ParseReleaseYear(album.ReleaseDate);

    return new Album(
      album.Id ?? string.Empty,
      albumArtist,
      string.IsNullOrWhiteSpace(album.Name) ? "Unknown Album" : album.Name,
      covers,
      releaseYear
    );
  }

  /// <summary>
  /// Maps a Spotify <see cref="FullAlbum"/> to an <see cref="Album"/>.
  /// </summary>
  /// <param name="album">The Spotify full album to map.</param>
  /// <param name="artist">Optional pre-mapped artist. If not provided, the first artist in the album is used.</param>
  /// <returns>The mapped SMoC <see cref="Album"/>.</returns>
  public static Album MapFullAlbumToAlbum(FullAlbum album, Artist? artist = null) {
    var albumArtist = artist ?? (album.Artists?.Count > 0 ? MapArtist(album.Artists[0]) : new Artist(string.Empty, "Unknown Artist"));
    var covers = MapImagesToCovers(album.Images);
    var releaseYear = ParseReleaseYear(album.ReleaseDate);

    return new Album(
      album.Id ?? string.Empty,
      albumArtist,
      string.IsNullOrWhiteSpace(album.Name) ? "Unknown Album" : album.Name,
      covers,
      releaseYear
    );
  }

  /// <summary>
  /// Maps a Spotify <see cref="FullTrack"/> to a <see cref="Song"/>.
  /// </summary>
  /// <param name="track">The Spotify full track to map.</param>
  /// <returns>The mapped SMoC <see cref="Song"/>.</returns>
  public static Song MapTrackToSong(FullTrack track) {
    var artist = track.Artists?.Count > 0
      ? MapArtist(track.Artists[0])
      : (track.Album?.Artists?.Count > 0
          ? MapArtist(track.Album.Artists[0])
          : new Artist(string.Empty, "Unknown Artist"));

    var album = track.Album != null
      ? MapSimpleAlbumToAlbum(track.Album, artist)
      : new Album(string.Empty, artist, "Unknown Album", []);

    return new Song(
      track.Id ?? string.Empty,
      album,
      string.IsNullOrWhiteSpace(track.Name) ? "Unknown Title" : track.Name,
      TimeSpan.FromMilliseconds(track.DurationMs),
      track.TrackNumber > 0 ? track.TrackNumber : null
    );
  }

  /// <summary>
  /// Maps a Spotify <see cref="SimpleTrack"/> to a <see cref="Song"/> with a pre-mapped album.
  /// </summary>
  /// <param name="track">The Spotify simple track to map.</param>
  /// <param name="album">The album the track belongs to.</param>
  /// <returns>The mapped SMoC <see cref="Song"/>.</returns>
  public static Song MapSimpleTrackToSong(SimpleTrack track, Album album) {
    return new Song(
      track.Id ?? string.Empty,
      album,
      string.IsNullOrWhiteSpace(track.Name) ? "Unknown Title" : track.Name,
      TimeSpan.FromMilliseconds(track.DurationMs),
      track.TrackNumber > 0 ? track.TrackNumber : null
    );
  }

  /// <summary>
  /// Parses the release year from a Spotify date string (e.g. "1981", "1981-12", "1981-12-15").
  /// </summary>
  /// <param name="releaseDate">The release date string.</param>
  /// <returns>The parsed year, or null if parsing fails.</returns>
  public static int? ParseReleaseYear(string? releaseDate) {
    if (string.IsNullOrWhiteSpace(releaseDate) || releaseDate.Length < 4) {
      return null;
    }

    if (int.TryParse(releaseDate.AsSpan(0, 4), out var year) && year > 0) {
      return year;
    }

    return null;
  }

  /// <summary>
  /// Maps a collection of Spotify <see cref="Image"/> objects to <see cref="AlbumCover"/> objects.
  /// </summary>
  /// <param name="images">The Spotify image collection.</param>
  /// <returns>A list of mapped <see cref="AlbumCover"/> objects.</returns>
  public static List<AlbumCover> MapImagesToCovers(IEnumerable<Image>? images) {
    if (images == null) {
      return [];
    }

    return images
      .Where(i => !string.IsNullOrEmpty(i.Url))
      .Select(i => new AlbumCover(i.Url, i.Width, i.Height))
      .ToList();
  }
}
