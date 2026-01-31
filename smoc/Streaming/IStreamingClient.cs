namespace Smoc.Streaming;

/// <summary>
/// Defines the contract for a music streaming service client.
/// </summary>
public interface IStreamingClient {
  /// <summary>
  /// Searches for artists matching the query.
  /// </summary>
  /// <param name="query">The search query.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A list of matching artists.</returns>
  Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default);

  /// <summary>
  /// Searches for songs matching the query.
  /// </summary>
  /// <param name="query">The search query.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A list of matching songs.</returns>
  Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves details for a specific song by ID.
  /// </summary>
  /// <param name="songId">The song ID.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>The song details.</returns>
  Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves details for a specific artist by ID.
  /// </summary>
  /// <param name="artistId">The artist ID.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>The artist details.</returns>
  Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves albums by a specific artist.
  /// </summary>
  /// <param name="artist">The artist.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A list of albums.</returns>
  Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves songs from a specific album.
  /// </summary>
  /// <param name="album">The album.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A list of songs.</returns>
  Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves the audio stream for a specific song by ID.
  /// </summary>
  /// <param name="songId">The song ID.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>The song stream.</returns>
  Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves the user's liked songs.
  /// </summary>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A list of liked songs.</returns>
  Task<List<Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Searches for playlists matching the query.
  /// </summary>
  /// <param name="query">The search query.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A list of matching playlists.</returns>
  Task<List<Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves songs from a specific playlist.
  /// </summary>
  /// <param name="playlist">The playlist.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A list of songs.</returns>
  Task<List<Song>> GetPlaylistSongsAsync(Playlist playlist, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves songs from the specified URL.
  /// </summary>
  /// <remarks>
  /// The results of this method should be interperted as a playlist by SMoC. However,
  /// there is no requirement that the URL actually points to a playlist, rather some
  /// collection of 1 or more songs.
  /// 
  /// This means the URL could be a playlist, an album, a song, or something else entirely.
  /// </remarks>
  /// <param name="url">The URL.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A list of songs.</returns>
  Task<List<Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default);

  /// <summary>
  /// Adds a song to the user's listen history.
  /// </summary>
  /// <param name="song">The song to add.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation.</returns>
  Task AddToListenHistory(Song song, CancellationToken cancellationToken = default);
}
