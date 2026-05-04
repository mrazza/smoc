
using Smoc.Streaming;
using Smoc.Streaming.Subsonic;
using Xunit;

namespace smoc.Tests.Streaming.Subsonic;

public class SubsonicMappingTest {
  private readonly SubsonicStreamingClient _client;

  public SubsonicMappingTest() {
    _client = SubsonicStreamingClient.CreateForTesting("http://localhost:8080", "user", "pass", true);
  }

  [Fact]
  public void MapSong_WithMinimalData_ReturnsCorrectSong() {
    var dto = new SongDto("song-1", "Test Song", "Test Album", "Test Artist", "artist-1", "album-1", 180, 1, "cover-1");
    var song = _client.MapSong(dto);

    Assert.Equal("song-1", song.Id);
    Assert.Equal("Test Song", song.Title);
    Assert.Equal(TimeSpan.FromSeconds(180), song.Duration);
    Assert.Equal(1, song.TrackNumber);
    Assert.Equal("Test Artist", song.Artist.Name);
    Assert.Equal("artist-1", song.Artist.Id);
    Assert.Equal("Test Album", song.Album.Name);
    Assert.Equal("album-1", song.Album.Id);
    
    // Verify cover art URL was built
    Assert.Single(song.Album.Covers);
    Assert.Contains("getCoverArt.view", song.Album.Covers.First().Url);
    Assert.Contains("id=cover-1", song.Album.Covers.First().Url);
  }

  [Fact]
  public void MapAlbum_ReturnsCorrectAlbum() {
    var artist = new Artist("artist-1", "Test Artist");
    var dto = new AlbumDto("album-1", "Test Album", "Test Artist", "artist-1", 10, 3600, "cover-1");
    var album = _client.MapAlbum(dto, artist);

    Assert.Equal("album-1", album.Id);
    Assert.Equal("Test Album", album.Name);
    Assert.Equal(artist, album.Artist);
    
    // Verify cover art URL was built
    Assert.Single(album.Covers);
    Assert.Contains("getCoverArt.view", album.Covers.First().Url);
    Assert.Contains("id=cover-1", album.Covers.First().Url);
  }
}
