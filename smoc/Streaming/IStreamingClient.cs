namespace Smoc.Streaming;

public interface IStreamingClient
{
    Task<List<Artist>> SearchArtistsAsync(string query);

    Task<Artist> GetArtistAsync(string artistId);

    Task<List<Album>> GetAlbumsByArtistAsync(Artist artist);

    Task<List<Song>> GetSongsByAlbumAsync(Album album);

    Task<SongStream> GetSongStreamAsync(string songId);
}