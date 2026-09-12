using Smoc.Streaming;
using Smoc.Streaming.Spotify.Util;
using SpotifyAPI.Web;
using Xunit;

namespace smoc.Tests.Streaming.Spotify;

/// <summary>
/// Unit tests for <see cref="SpotifyMapper"/>.
/// </summary>
public class SpotifyMappingTest {
  /// <summary>
  /// Verifies mapping of a valid <see cref="SimpleArtist"/>.
  /// </summary>
  [Fact]
  public void MapArtist_WithSimpleArtist_MapsCorrectly() {
    var simpleArtist = new SimpleArtist {
      Id = "artist-1",
      Name = "Daft Punk"
    };

    var artist = SpotifyMapper.MapArtist(simpleArtist);

    Assert.Equal("artist-1", artist.Id);
    Assert.Equal("Daft Punk", artist.Name);
  }

  /// <summary>
  /// Verifies mapping of a null <see cref="SimpleArtist"/> fallback.
  /// </summary>
  [Fact]
  public void MapArtist_WithNullSimpleArtist_ReturnsUnknown() {
    var artist = SpotifyMapper.MapArtist((SimpleArtist?)null);

    Assert.Equal(string.Empty, artist.Id);
    Assert.Equal("Unknown Artist", artist.Name);
  }

  /// <summary>
  /// Verifies mapping of a valid <see cref="FullArtist"/>.
  /// </summary>
  [Fact]
  public void MapArtist_WithFullArtist_MapsCorrectly() {
    var fullArtist = new FullArtist {
      Id = "artist-2",
      Name = "Kraftwerk"
    };

    var artist = SpotifyMapper.MapArtist(fullArtist);

    Assert.Equal("artist-2", artist.Id);
    Assert.Equal("Kraftwerk", artist.Name);
  }

  /// <summary>
  /// Verifies mapping of a null <see cref="FullArtist"/> fallback.
  /// </summary>
  [Fact]
  public void MapArtist_WithNullFullArtist_ReturnsUnknown() {
    var artist = SpotifyMapper.MapArtist((FullArtist?)null);

    Assert.Equal(string.Empty, artist.Id);
    Assert.Equal("Unknown Artist", artist.Name);
  }

  /// <summary>
  /// Verifies mapping of a <see cref="SimpleAlbum"/> with full metadata.
  /// </summary>
  [Fact]
  public void MapSimpleAlbumToAlbum_WithFullMetadata_MapsCorrectly() {
    var album = new SimpleAlbum {
      Id = "album-1",
      Name = "Discovery",
      ReleaseDate = "2001-03-12",
      Artists = [new SimpleArtist { Id = "art-1", Name = "Daft Punk" }],
      Images = [
        new Image { Url = "https://img.spotify.com/300.jpg", Width = 300, Height = 300 },
        new Image { Url = "https://img.spotify.com/64.jpg", Width = 64, Height = 64 }
      ]
    };

    var mapped = SpotifyMapper.MapSimpleAlbumToAlbum(album);

    Assert.Equal("album-1", mapped.Id);
    Assert.Equal("Discovery", mapped.Name);
    Assert.Equal(2001, mapped.ReleaseYear);
    Assert.Equal("art-1", mapped.Artist.Id);
    Assert.Equal("Daft Punk", mapped.Artist.Name);
    Assert.Equal(2, mapped.Covers.Count());
    var firstCover = mapped.Covers.First();
    Assert.Equal("https://img.spotify.com/300.jpg", firstCover.Url);
    Assert.Equal(300, firstCover.Width);
    Assert.Equal(300, firstCover.Height);
  }

  /// <summary>
  /// Verifies mapping of a <see cref="SimpleAlbum"/> with null/empty collections.
  /// </summary>
  [Fact]
  public void MapSimpleAlbumToAlbum_WithNullCollections_HandlesDefensively() {
    var album = new SimpleAlbum {
      Id = null!,
      Name = null!,
      ReleaseDate = null!,
      Artists = null!,
      Images = null!
    };

    var mapped = SpotifyMapper.MapSimpleAlbumToAlbum(album);

    Assert.Equal(string.Empty, mapped.Id);
    Assert.Equal("Unknown Album", mapped.Name);
    Assert.Null(mapped.ReleaseYear);
    Assert.Equal(string.Empty, mapped.Artist.Id);
    Assert.Equal("Unknown Artist", mapped.Artist.Name);
    Assert.Empty(mapped.Covers);
  }

  /// <summary>
  /// Verifies mapping of a <see cref="FullAlbum"/> with full metadata.
  /// </summary>
  [Fact]
  public void MapFullAlbumToAlbum_WithFullMetadata_MapsCorrectly() {
    var album = new FullAlbum {
      Id = "album-full-1",
      Name = "Random Access Memories",
      ReleaseDate = "2013-05-17",
      Artists = [new SimpleArtist { Id = "art-1", Name = "Daft Punk" }],
      Images = [new Image { Url = "https://img.spotify.com/640.jpg", Width = 640, Height = 640 }]
    };

    var mapped = SpotifyMapper.MapFullAlbumToAlbum(album);

    Assert.Equal("album-full-1", mapped.Id);
    Assert.Equal("Random Access Memories", mapped.Name);
    Assert.Equal(2013, mapped.ReleaseYear);
    Assert.Equal("art-1", mapped.Artist.Id);
    Assert.Equal("Daft Punk", mapped.Artist.Name);
    Assert.Single(mapped.Covers);
  }

  /// <summary>
  /// Verifies mapping of a <see cref="FullTrack"/> with full metadata.
  /// </summary>
  [Fact]
  public void MapTrackToSong_WithFullTrack_MapsCorrectly() {
    var track = new FullTrack {
      Id = "track-1",
      Name = "One More Time",
      DurationMs = 320000,
      TrackNumber = 1,
      Artists = [new SimpleArtist { Id = "art-1", Name = "Daft Punk" }],
      Album = new SimpleAlbum {
        Id = "alb-1",
        Name = "Discovery",
        ReleaseDate = "2001",
        Artists = [new SimpleArtist { Id = "art-1", Name = "Daft Punk" }],
        Images = [new Image { Url = "https://img.spotify.com/cover.jpg", Width = 300, Height = 300 }]
      }
    };

    var song = SpotifyMapper.MapTrackToSong(track);

    Assert.Equal("track-1", song.Id);
    Assert.Equal("One More Time", song.Title);
    Assert.Equal(TimeSpan.FromMilliseconds(320000), song.Duration);
    Assert.Equal(1, song.TrackNumber);
    Assert.Equal("art-1", song.Artist.Id);
    Assert.Equal("Daft Punk", song.Artist.Name);
    Assert.Equal("alb-1", song.Album.Id);
    Assert.Equal("Discovery", song.Album.Name);
    Assert.Equal(2001, song.Album.ReleaseYear);
  }

  /// <summary>
  /// Verifies mapping of a <see cref="FullTrack"/> with null/empty artists and album.
  /// </summary>
  [Fact]
  public void MapTrackToSong_WithNullProperties_HandlesDefensively() {
    var track = new FullTrack {
      Id = null!,
      Name = null!,
      DurationMs = 0,
      TrackNumber = 0,
      Artists = null!,
      Album = null!
    };

    var song = SpotifyMapper.MapTrackToSong(track);

    Assert.Equal(string.Empty, song.Id);
    Assert.Equal("Unknown Title", song.Title);
    Assert.Equal(TimeSpan.Zero, song.Duration);
    Assert.Null(song.TrackNumber);
    Assert.Equal(string.Empty, song.Artist.Id);
    Assert.Equal("Unknown Artist", song.Artist.Name);
    Assert.Equal(string.Empty, song.Album.Id);
    Assert.Equal("Unknown Album", song.Album.Name);
  }

  /// <summary>
  /// Verifies mapping of a <see cref="FullTrack"/> where artist is fallback from album artists.
  /// </summary>
  [Fact]
  public void MapTrackToSong_FallsBackToAlbumArtist_WhenTrackArtistsEmpty() {
    var track = new FullTrack {
      Id = "track-2",
      Name = "Aerodynamic",
      DurationMs = 210000,
      Artists = [],
      Album = new SimpleAlbum {
        Id = "alb-1",
        Name = "Discovery",
        Artists = [new SimpleArtist { Id = "art-fallback", Name = "Album Artist" }]
      }
    };

    var song = SpotifyMapper.MapTrackToSong(track);

    Assert.Equal("art-fallback", song.Artist.Id);
    Assert.Equal("Album Artist", song.Artist.Name);
  }

  /// <summary>
  /// Verifies mapping of a <see cref="SimpleTrack"/>.
  /// </summary>
  [Fact]
  public void MapSimpleTrackToSong_MapsCorrectly() {
    var artist = new Artist("art-1", "Daft Punk");
    var album = new Album("alb-1", artist, "Discovery", []);
    var simpleTrack = new SimpleTrack {
      Id = "simple-1",
      Name = "Digital Love",
      DurationMs = 301000,
      TrackNumber = 3
    };

    var song = SpotifyMapper.MapSimpleTrackToSong(simpleTrack, album);

    Assert.Equal("simple-1", song.Id);
    Assert.Equal("Digital Love", song.Title);
    Assert.Equal(album, song.Album);
    Assert.Equal(3, song.TrackNumber);
    Assert.Equal(TimeSpan.FromMilliseconds(301000), song.Duration);
  }

  /// <summary>
  /// Verifies parsing of various release date formats.
  /// </summary>
  /// <param name="dateString">The input date string.</param>
  /// <param name="expectedYear">The expected parsed year.</param>
  [Theory]
  [InlineData("1994", 1994)]
  [InlineData("1994-03-08", 1994)]
  [InlineData("1994-03", 1994)]
  [InlineData("2026-12-31", 2026)]
  [InlineData(null, null)]
  [InlineData("", null)]
  [InlineData("   ", null)]
  [InlineData("abc", null)]
  [InlineData("invalid-date", null)]
  [InlineData("-100", null)]
  [InlineData("0000", null)]
  public void ParseReleaseYear_WithVariousInputs_ReturnsExpected(string? dateString, int? expectedYear) {
    var result = SpotifyMapper.ParseReleaseYear(dateString);
    Assert.Equal(expectedYear, result);
  }

  /// <summary>
  /// Verifies mapping of images to covers and defensive handling of null or empty URLs.
  /// </summary>
  [Fact]
  public void MapImagesToCovers_HandlesNullAndEmptyCorrectly() {
    Assert.Empty(SpotifyMapper.MapImagesToCovers(null));
    Assert.Empty(SpotifyMapper.MapImagesToCovers([]));

    var images = new List<Image> {
      new Image { Url = "https://example.com/img1.jpg", Width = 300, Height = 300 },
      new Image { Url = "", Width = 100, Height = 100 },
      new Image { Url = null!, Width = 50, Height = 50 },
      new Image { Url = "https://example.com/img2.jpg", Width = 600, Height = 600 }
    };

    var covers = SpotifyMapper.MapImagesToCovers(images);

    Assert.Equal(2, covers.Count);
    Assert.Equal("https://example.com/img1.jpg", covers[0].Url);
    Assert.Equal(300, covers[0].Width);
    Assert.Equal(300, covers[0].Height);
    Assert.Equal("https://example.com/img2.jpg", covers[1].Url);
    Assert.Equal(600, covers[1].Width);
    Assert.Equal(600, covers[1].Height);
  }
}
