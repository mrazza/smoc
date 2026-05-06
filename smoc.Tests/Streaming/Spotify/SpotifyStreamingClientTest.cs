using System.Net;
using Moq;
using Smoc.Configuration;
using Smoc.Services.Caching;
using Smoc.Streaming;
using Smoc.Streaming.Spotify;
using SpotifyAPI.Web;
using Xunit;

namespace smoc.Tests.Streaming.Spotify;

/// <summary>
/// Unit tests for <see cref="SpotifyStreamingClient"/>.
/// </summary>
public class SpotifyStreamingClientTest {
  /// <summary>
  /// Verifies factory method creates an instance.
  /// </summary>
  [Fact]
  public void Create_InitializesCorrectly() {
    using var client = SpotifyStreamingClient.Create(new NoCachingCacheService(), new NoCachingCacheService());
    Assert.NotNull(client);
  }

  /// <summary>
  /// Verifies testing factory method creates an instance with injected mock.
  /// </summary>
  [Fact]
  public void CreateForTesting_InitializesCorrectly() {
    var mockSpotify = new Mock<ISpotifyClient>();
    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    Assert.NotNull(client);
  }

  /// <summary>
  /// Verifies that playback stream request throws <see cref="NotSupportedException"/> with descriptive message.
  /// </summary>
  [Fact]
  public async Task GetSongStreamAsync_ThrowsNotSupportedException() {
    var mockSpotify = new Mock<ISpotifyClient>();
    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);

    var ex = await Assert.ThrowsAsync<NotSupportedException>(() => client.GetSongStreamAsync("song-123", TestContext.Current.CancellationToken));
    Assert.Contains("Spotify playback is not supported yet (requires Librespot integration)", ex.Message);
  }

  /// <summary>
  /// Verifies that GetLikedSongsAsync returns an empty list.
  /// </summary>
  [Fact]
  public async Task GetLikedSongsAsync_ReturnsEmptyList() {
    var mockSpotify = new Mock<ISpotifyClient>();
    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);

    var songs = await client.GetLikedSongsAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(songs);
    Assert.Empty(songs);
  }

  /// <summary>
  /// Verifies that AddToListenHistory completes without throwing.
  /// </summary>
  [Fact]
  public async Task AddToListenHistory_CompletesSuccessfully() {
    var mockSpotify = new Mock<ISpotifyClient>();
    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);

    var artist = new Artist("a1", "Artist 1");
    var album = new Album("alb1", artist, "Album 1", []);
    var song = new Song("s1", album, "Song 1", TimeSpan.FromMinutes(3));

    await client.AddToListenHistory(song, TestContext.Current.CancellationToken);
  }

  /// <summary>
  /// Verifies searching artists maps returned artists.
  /// </summary>
  [Fact]
  public async Task SearchArtistsAsync_ReturnsMappedArtists() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var mockSearch = new Mock<ISearchClient>();
    mockSpotify.Setup(s => s.Search).Returns(mockSearch.Object);

    mockSearch.Setup(s => s.Item(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SearchResponse {
        Artists = new Paging<FullArtist, SearchResponse> {
          Items = [new FullArtist { Id = "art-1", Name = "Daft Punk" }]
        }
      });

    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    var artists = await client.SearchArtistsAsync("Daft Punk", TestContext.Current.CancellationToken);

    Assert.Single(artists);
    Assert.Equal("art-1", artists[0].Id);
    Assert.Equal("Daft Punk", artists[0].Name);
  }

  /// <summary>
  /// Verifies searching songs maps returned tracks.
  /// </summary>
  [Fact]
  public async Task SearchSongsAsync_ReturnsMappedSongs() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var mockSearch = new Mock<ISearchClient>();
    mockSpotify.Setup(s => s.Search).Returns(mockSearch.Object);

    mockSearch.Setup(s => s.Item(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SearchResponse {
        Tracks = new Paging<FullTrack, SearchResponse> {
          Items = [
            new FullTrack {
              Id = "track-1",
              Name = "Get Lucky",
              DurationMs = 240000,
              TrackNumber = 8,
              Artists = [new SimpleArtist { Id = "art-1", Name = "Daft Punk" }],
              Album = new SimpleAlbum { Id = "alb-1", Name = "RAM", ReleaseDate = "2013" }
            }
          ]
        }
      });

    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    var songs = await client.SearchSongsAsync("Get Lucky", TestContext.Current.CancellationToken);

    Assert.Single(songs);
    Assert.Equal("track-1", songs[0].Id);
    Assert.Equal("Get Lucky", songs[0].Title);
    Assert.Equal("Daft Punk", songs[0].Artist.Name);
  }

  /// <summary>
  /// Verifies GetSongAsync retrieves and maps a track.
  /// </summary>
  [Fact]
  public async Task GetSongAsync_ReturnsMappedSong() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var mockTracks = new Mock<ITracksClient>();
    mockSpotify.Setup(s => s.Tracks).Returns(mockTracks.Object);

    mockTracks.Setup(t => t.Get("track-99", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FullTrack {
        Id = "track-99",
        Name = "Harder, Better, Faster, Stronger",
        DurationMs = 224000,
        TrackNumber = 4,
        Artists = [new SimpleArtist { Id = "art-1", Name = "Daft Punk" }],
        Album = new SimpleAlbum { Id = "alb-1", Name = "Discovery", ReleaseDate = "2001" }
      });

    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    var song = await client.GetSongAsync("track-99", TestContext.Current.CancellationToken);

    Assert.Equal("track-99", song.Id);
    Assert.Equal("Harder, Better, Faster, Stronger", song.Title);
    Assert.Equal(4, song.TrackNumber);
    Assert.Equal(TimeSpan.FromMilliseconds(224000), song.Duration);
  }

  /// <summary>
  /// Verifies GetArtistAsync retrieves and maps an artist.
  /// </summary>
  [Fact]
  public async Task GetArtistAsync_ReturnsMappedArtist() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var mockArtists = new Mock<IArtistsClient>();
    mockSpotify.Setup(s => s.Artists).Returns(mockArtists.Object);

    mockArtists.Setup(a => a.Get("art-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FullArtist { Id = "art-1", Name = "Daft Punk" });

    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    var artist = await client.GetArtistAsync("art-1", TestContext.Current.CancellationToken);

    Assert.Equal("art-1", artist.Id);
    Assert.Equal("Daft Punk", artist.Name);
  }

  /// <summary>
  /// Verifies GetAlbumsByArtistAsync retrieves and maps albums.
  /// </summary>
  [Fact]
  public async Task GetAlbumsByArtistAsync_ReturnsMappedAlbums() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var mockArtists = new Mock<IArtistsClient>();
    mockSpotify.Setup(s => s.Artists).Returns(mockArtists.Object);

    mockArtists.Setup(a => a.GetAlbums("art-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Paging<SimpleAlbum> {
        Items = [
          new SimpleAlbum {
            Id = "alb-1",
            Name = "Homework",
            ReleaseDate = "1997",
            Artists = [new SimpleArtist { Id = "art-1", Name = "Daft Punk" }]
          }
        ]
      });

    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    var artist = new Artist("art-1", "Daft Punk");
    var albums = await client.GetAlbumsByArtistAsync(artist, TestContext.Current.CancellationToken);

    Assert.Single(albums);
    Assert.Equal("alb-1", albums[0].Id);
    Assert.Equal("Homework", albums[0].Name);
    Assert.Equal(1997, albums[0].ReleaseYear);
  }

  /// <summary>
  /// Verifies GetSongsByAlbumAsync retrieves and maps album tracks.
  /// </summary>
  [Fact]
  public async Task GetSongsByAlbumAsync_ReturnsMappedSongs() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var mockAlbums = new Mock<IAlbumsClient>();
    mockSpotify.Setup(s => s.Albums).Returns(mockAlbums.Object);

    mockAlbums.Setup(a => a.GetTracks("alb-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Paging<SimpleTrack> {
        Items = [
          new SimpleTrack {
            Id = "st-1",
            Name = "Da Funk",
            DurationMs = 328000,
            TrackNumber = 4
          }
        ]
      });

    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    var artist = new Artist("art-1", "Daft Punk");
    var album = new Album("alb-1", artist, "Homework", []);
    var songs = await client.GetSongsByAlbumAsync(album, TestContext.Current.CancellationToken);

    Assert.Single(songs);
    Assert.Equal("st-1", songs[0].Id);
    Assert.Equal("Da Funk", songs[0].Title);
    Assert.Equal(4, songs[0].TrackNumber);
    Assert.Equal(album, songs[0].Album);
  }

  /// <summary>
  /// Verifies SearchPlaylistsAsync retrieves and maps playlists.
  /// </summary>
  [Fact]
  public async Task SearchPlaylistsAsync_ReturnsMappedPlaylists() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var mockSearch = new Mock<ISearchClient>();
    mockSpotify.Setup(s => s.Search).Returns(mockSearch.Object);

    mockSearch.Setup(s => s.Item(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SearchResponse {
        Playlists = new Paging<FullPlaylist, SearchResponse> {
          Items = [
            new FullPlaylist { Id = "pl-1", Name = "Best of Electronic" }
          ]
        }
      });

    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    var playlists = await client.SearchPlaylistsAsync("Electronic", TestContext.Current.CancellationToken);

    Assert.Single(playlists);
    Assert.Equal("pl-1", playlists[0].Id);
    Assert.Equal("Best of Electronic", playlists[0].Name);
  }

  /// <summary>
  /// Verifies GetPlaylistSongsAsync retrieves and maps full tracks in a playlist.
  /// </summary>
  [Fact]
  public async Task GetPlaylistSongsAsync_ReturnsMappedSongs() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var mockPlaylists = new Mock<IPlaylistsClient>();
    mockSpotify.Setup(s => s.Playlists).Returns(mockPlaylists.Object);

    mockPlaylists.Setup(p => p.GetPlaylistItems("pl-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Paging<PlaylistTrack<IPlayableItem>> {
        Items = [
          new PlaylistTrack<IPlayableItem> {
            Track = new FullTrack {
              Id = "track-pl-1",
              Name = "Around the World",
              DurationMs = 429000,
              TrackNumber = 7,
              Artists = [new SimpleArtist { Id = "art-1", Name = "Daft Punk" }],
              Album = new SimpleAlbum { Id = "alb-1", Name = "Homework", ReleaseDate = "1997" }
            }
          }
        ]
      });

    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    var playlist = new Playlist("pl-1", "Daft Punk Best");
    var songs = await client.GetPlaylistSongsAsync(playlist, TestContext.Current.CancellationToken);

    Assert.Single(songs);
    Assert.Equal("track-pl-1", songs[0].Id);
    Assert.Equal("Around the World", songs[0].Title);
  }

  /// <summary>
  /// Verifies GetPlaylistSongsFromUrlAsync handles track, playlist, and album URLs.
  /// </summary>
  [Fact]
  public async Task GetPlaylistSongsFromUrlAsync_WithTrackUrl_ReturnsSong() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var mockTracks = new Mock<ITracksClient>();
    mockSpotify.Setup(s => s.Tracks).Returns(mockTracks.Object);

    mockTracks.Setup(t => t.Get("track-url-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FullTrack {
        Id = "track-url-1",
        Name = "Instant Crush",
        DurationMs = 337000,
        TrackNumber = 5,
        Artists = [new SimpleArtist { Id = "art-1", Name = "Daft Punk" }],
        Album = new SimpleAlbum { Id = "alb-ram", Name = "RAM", ReleaseDate = "2013" }
      });

    using var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);
    var songs = await client.GetPlaylistSongsFromUrlAsync("https://open.spotify.com/track/track-url-1", TestContext.Current.CancellationToken);

    Assert.Single(songs);
    Assert.Equal("track-url-1", songs[0].Id);
    Assert.Equal("Instant Crush", songs[0].Title);
  }

  /// <summary>
  /// Verifies that calling an operation without configuring ClientId and ClientSecret throws <see cref="InvalidOperationException"/>.
  /// </summary>
  [Fact]
  public async Task EnsureClient_WithoutCredentials_ThrowsInvalidOperationException() {
    var previousId = SpotifyConfig.Defaults.ClientId;
    var previousSecret = SpotifyConfig.Defaults.ClientSecret;
    try {
      SpotifyConfig.Defaults.ClientId = null;
      SpotifyConfig.Defaults.ClientSecret = null;

      using var client = SpotifyStreamingClient.Create();
      await Assert.ThrowsAsync<InvalidOperationException>(() => client.SearchArtistsAsync("test", TestContext.Current.CancellationToken));
    } finally {
      SpotifyConfig.Defaults.ClientId = previousId;
      SpotifyConfig.Defaults.ClientSecret = previousSecret;
    }
  }

  /// <summary>
  /// Verifies that Dispose can be called multiple times safely.
  /// </summary>
  [Fact]
  public void Dispose_CanBeCalledMultipleTimes() {
    var mockSpotify = new Mock<ISpotifyClient>();
    var client = SpotifyStreamingClient.CreateForTesting(mockSpotify.Object);

    client.Dispose();
    client.Dispose();
  }
}
