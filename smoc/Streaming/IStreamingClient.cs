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
}
