namespace Smoc.Streaming;

public interface IStreamingClient {
  Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default);

  Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default);

  Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default);

  Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default);

  Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default);

  Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default);

  Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default);
}
