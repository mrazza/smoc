namespace Smoc.Streaming.Subsonic.Util;

/// <summary>
/// Utility class for mapping Subsonic models to SMoC streaming models.
/// </summary>
public static class SubsonicMapper {
  /// <summary>
  /// Maps a Subsonic artist to a SMoC artist.
  /// </summary>
  public static Artist MapArtist(Models.Artist subsonicArtist) {
    return new Artist(subsonicArtist.Id, subsonicArtist.Name);
  }

  /// <summary>
  /// Maps a Subsonic artist (with albums) to a SMoC artist.
  /// </summary>
  public static Artist MapArtist(Models.ArtistWithAlbums subsonicArtist) {
    return new Artist(subsonicArtist.Id, subsonicArtist.Name);
  }

  /// <summary>
  /// Maps a Subsonic playlist to a SMoC playlist.
  /// </summary>
  public static Playlist MapPlaylist(Models.Playlist subsonicPlaylist) {
    return new Playlist(subsonicPlaylist.Id, subsonicPlaylist.Name);
  }

  /// <summary>
  /// Maps a Subsonic album to a SMoC album.
  /// </summary>
  public static Album MapAlbum(Models.Album subsonicAlbum, Artist artist, Func<string, string> coverArtUrlBuilder) {
    return new Album(
      subsonicAlbum.Id,
      artist,
      subsonicAlbum.Name,
      subsonicAlbum.CoverArt != null ? [new AlbumCover(coverArtUrlBuilder(subsonicAlbum.CoverArt), 0, 0)] : []
    );
  }

  /// <summary>
  /// Maps a Subsonic song to a SMoC song.
  /// </summary>
  public static Song MapSong(Models.Song subsonicSong, Func<string, string> coverArtUrlBuilder) {
    var artist = new Artist(subsonicSong.ArtistId ?? "", subsonicSong.ArtistName ?? "Unknown Artist");
    var album = new Album(
      subsonicSong.AlbumId ?? "",
      artist,
      subsonicSong.AlbumName ?? "Unknown Album",
      subsonicSong.CoverArt != null ? [new AlbumCover(coverArtUrlBuilder(subsonicSong.CoverArt), 0, 0)] : []
    );
    return MapSong(subsonicSong, album);
  }

  /// <summary>
  /// Maps a Subsonic song to a SMoC song with a pre-mapped album.
  /// </summary>
  public static Song MapSong(Models.Song subsonicSong, Album album) {
    return new Song(subsonicSong.Id, album, subsonicSong.Title, TimeSpan.FromSeconds(subsonicSong.Duration ?? 0), subsonicSong.Track);
  }
}
