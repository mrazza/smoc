using Smoc.Streaming.Tidal;
using Smoc.Streaming.Tidal.Models;
using Xunit;

namespace smoc.Tests.Streaming.Tidal;

public class TidalMappingTest {
  [Fact]
  public void MapTrackToSong_FullMetadata_MapsCorrectly() {
    var artist = new TidalArtist(42, "Test Artist", "pic-id");
    var album = new TidalAlbum(101, "Test Album", "cover-uuid-1234", "2024-05-15", artist);
    var track = new TidalTrack(999, "Test Track", 215, 3, album, artist, [artist]);

    var song = TidalStreamingClient.MapTrackToSong(track);

    Assert.Equal("999", song.Id);
    Assert.Equal("Test Track", song.Title);
    Assert.Equal(TimeSpan.FromSeconds(215), song.Duration);
    Assert.Equal(3, song.TrackNumber);
    Assert.Equal("Test Artist", song.Artist.Name);
    Assert.Equal("42", song.Artist.Id);
    Assert.Equal("101", song.Album.Id);
    Assert.Equal("Test Album", song.Album.Name);
    Assert.Equal(2024, song.Album.ReleaseYear);
    Assert.Equal(2, song.Album.Covers.Count());
  }

  [Fact]
  public void MapTrackToSong_NullArtist_FallsBackToAlbumArtist() {
    var albumArtist = new TidalArtist(55, "Album Artist", null);
    var album = new TidalAlbum(202, "Album Name", null, "2020-01-01", albumArtist);
    var track = new TidalTrack(888, "Track Name", 180, 1, album, null, null);

    var song = TidalStreamingClient.MapTrackToSong(track);

    Assert.Equal("Album Artist", song.Artist.Name);
    Assert.Equal("55", song.Artist.Id);
    Assert.Empty(song.Album.Covers);
    Assert.Equal(2020, song.Album.ReleaseYear);
  }

  [Fact]
  public void MapTrackToSong_NullArtistAndAlbum_DoesNotThrow() {
    var track = new TidalTrack(777, "Standalone Track", 120, 1, null, null, null);

    var song = TidalStreamingClient.MapTrackToSong(track);

    Assert.Equal("777", song.Id);
    Assert.Equal("Standalone Track", song.Title);
    Assert.Equal("Unknown Artist", song.Artist.Name);
    Assert.Equal("Unknown Album", song.Album.Name);
    Assert.Empty(song.Album.Covers);
    Assert.Null(song.Album.ReleaseYear);
  }

  [Fact]
  public void MapAlbumToAlbum_NullCoverAndInvalidReleaseDate_HandlesGracefully() {
    var tidalAlbum = new TidalAlbum(303, "Album Without Cover", null, "invalid-date", null);

    var album = TidalStreamingClient.MapAlbumToAlbum(tidalAlbum);

    Assert.Equal("303", album.Id);
    Assert.Equal("Album Without Cover", album.Name);
    Assert.Empty(album.Covers);
    Assert.Null(album.ReleaseYear);
    Assert.Equal("Unknown Artist", album.Artist.Name);
  }

  [Fact]
  public void MapAlbumToAlbum_CoverFormattedWithSlashes() {
    var tidalAlbum = new TidalAlbum(404, "Cover Album", "ab-cd-ef", "1999-12-31", null);

    var album = TidalStreamingClient.MapAlbumToAlbum(tidalAlbum);

    Assert.NotEmpty(album.Covers);
    Assert.Contains("ab/cd/ef", album.Covers.First().Url);
    Assert.Equal(1999, album.ReleaseYear);
  }
}
