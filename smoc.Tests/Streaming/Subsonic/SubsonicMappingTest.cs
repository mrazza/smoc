using Smoc.Streaming;
using Smoc.Streaming.Subsonic.Models;
using Smoc.Streaming.Subsonic.Util;
using Xunit;

namespace smoc.Tests.Streaming.Subsonic;

using SubsonicModels = Smoc.Streaming.Subsonic.Models;

public class SubsonicMappingTest {
  private string MockUrlBuilder(string id) => $"http://localhost/art?id={id}";

  [Fact]
  public void MapSong_WithMinimalData_ReturnsCorrectSong() {
    var dto = new SubsonicModels.Song("song-1", "Test Song", "Test Album", "Test Artist", "artist-1", "album-1", 180, 1, "cover-1");
    var song = SubsonicMapper.MapSong(dto, MockUrlBuilder);

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
    Assert.Equal("http://localhost/art?id=cover-1", song.Album.Covers.First().Url);
  }

  [Fact]
  public void MapSong_WithNoCoverArt_ReturnsEmptyCovers() {
    var dto = new SubsonicModels.Song("song-1", "Test Song", "Test Album", "Test Artist", "artist-1", "album-1", 180, 1, null);
    var song = SubsonicMapper.MapSong(dto, MockUrlBuilder);

    Assert.Empty(song.Album.Covers);
  }

  [Fact]
  public void MapSong_WithPreMappedAlbum_ReturnsCorrectSong() {
    var artist = new Smoc.Streaming.Artist("artist-1", "Test Artist");
    var album = new Smoc.Streaming.Album("album-1", artist, "Test Album", []);
    var dto = new SubsonicModels.Song("song-1", "Test Song", "Test Album", "Test Artist", "artist-1", "album-1", 180, 1, "cover-1");
    
    var song = SubsonicMapper.MapSong(dto, album);

    Assert.Equal("song-1", song.Id);
    Assert.Equal(album, song.Album);
    Assert.Equal(TimeSpan.FromSeconds(180), song.Duration);
  }

  [Fact]
  public void MapAlbum_ReturnsCorrectAlbum() {
    var artist = new Smoc.Streaming.Artist("artist-1", "Test Artist");
    var dto = new SubsonicModels.Album("album-1", "Test Album", "Test Artist", "artist-1", 10, 3600, "cover-1");
    var album = SubsonicMapper.MapAlbum(dto, artist, MockUrlBuilder);

    Assert.Equal("album-1", album.Id);
    Assert.Equal("Test Album", album.Name);
    Assert.Equal(artist, album.Artist);
    
    // Verify cover art URL was built
    Assert.Single(album.Covers);
    Assert.Equal("http://localhost/art?id=cover-1", album.Covers.First().Url);
  }

  [Fact]
  public void MapAlbum_WithNoCoverArt_ReturnsEmptyCovers() {
    var artist = new Smoc.Streaming.Artist("artist-1", "Test Artist");
    var dto = new SubsonicModels.Album("album-1", "Test Album", "Test Artist", "artist-1", 10, 3600, null);
    var album = SubsonicMapper.MapAlbum(dto, artist, MockUrlBuilder);

    Assert.Empty(album.Covers);
  }

  [Fact]
  public void MapArtist_ReturnsCorrectArtist() {
    var dto = new SubsonicModels.Artist("artist-1", "Test Artist", 5);
    var artist = SubsonicMapper.MapArtist(dto);

    Assert.Equal("artist-1", artist.Id);
    Assert.Equal("Test Artist", artist.Name);
  }

  [Fact]
  public void MapArtist_WithAlbums_ReturnsCorrectArtist() {
    var dto = new SubsonicModels.ArtistWithAlbums("artist-1", "Test Artist", []);
    var artist = SubsonicMapper.MapArtist(dto);

    Assert.Equal("artist-1", artist.Id);
    Assert.Equal("Test Artist", artist.Name);
  }

  [Fact]
  public void MapPlaylist_ReturnsCorrectPlaylist() {
    var dto = new SubsonicModels.Playlist("playlist-1", "Test Playlist", "user", 10, 3600);
    var playlist = SubsonicMapper.MapPlaylist(dto);

    Assert.Equal("playlist-1", playlist.Id);
    Assert.Equal("Test Playlist", playlist.Name);
  }
}
