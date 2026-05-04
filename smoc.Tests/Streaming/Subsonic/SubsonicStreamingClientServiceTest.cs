using System.Net;
using Moq;
using Moq.Protected;
using Smoc.Configuration;
using Smoc.Streaming.Subsonic;
using Smoc.Streaming;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Services.Caching;

namespace smoc.Tests.Streaming.Subsonic;

public class SubsonicStreamingClientServiceTest {
  private static SubsonicStreamingClient CreateClientWithMockResponse(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK, bool useToken = true) {
    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = statusCode,
        Content = new StringContent(jsonResponse),
      });

    var httpClient = new HttpClient(handlerMock.Object);
    var client = SubsonicStreamingClient.CreateForTesting("localhost", "user", "pass", useToken);
    var httpClientField = typeof(SubsonicStreamingClient).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    httpClientField?.SetValue(client, httpClient);
    return client;
  }

  private static SubsonicStreamingClient CreateClientWithMockStreamResponse(byte[] contentBytes) {
    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK,
        Content = new ByteArrayContent(contentBytes),
      });

    var httpClient = new HttpClient(handlerMock.Object);
    var client = SubsonicStreamingClient.CreateForTesting("localhost", "user", "pass", true);
    var httpClientField = typeof(SubsonicStreamingClient).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    httpClientField?.SetValue(client, httpClient);
    return client;
  }

  [Fact]
  public async Task SearchArtistsAsync_ReturnsArtistsOnSuccess() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""searchResult3"": {
          ""artist"": [
            { ""id"": ""a1"", ""name"": ""Artist One"" },
            { ""id"": ""a2"", ""name"": ""Artist Two"" }
          ]
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.SearchArtistsAsync("Artist", TestContext.Current.CancellationToken);
    Assert.Equal(2, results.Count);
    Assert.Equal("Artist One", results[0].Name);
    Assert.Equal("a1", results[0].Id);
  }

  [Fact]
  public async Task SearchArtistsAsync_ReturnsEmptyWhenMissing() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""searchResult3"": {}
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.SearchArtistsAsync("Artist", TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }
  
  [Fact]
  public async Task SearchArtistsAsync_ReturnsEmptyWhenNoSearchResult3() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.SearchArtistsAsync("Artist", TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetAsync_ThrowsOnSubsonicError() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""failed"",
        ""version"": ""1.16.1"",
        ""error"": { ""code"": 40, ""message"": ""Wrong user or password"" }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var ex = await Assert.ThrowsAsync<Exception>(() => client.SearchArtistsAsync("Artist", TestContext.Current.CancellationToken));
    Assert.Contains("error 40: Wrong user or password", ex.Message);
  }
  
  [Fact]
  public async Task GetAsync_ThrowsOnSubsonicError_NoCodeOrMessage() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""failed"",
        ""version"": ""1.16.1"",
        ""error"": {}
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var ex = await Assert.ThrowsAsync<Exception>(() => client.SearchArtistsAsync("Artist", TestContext.Current.CancellationToken));
    Assert.Contains("error 0: Unknown Subsonic error", ex.Message);
  }

  [Fact]
  public async Task GetResponseElementAsync_ThrowsOnMissingSubsonicResponse() {
    var jsonResponse = @"{ ""wrong-response"": {} }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var ex = await Assert.ThrowsAsync<Exception>(() => client.SearchArtistsAsync("Artist", TestContext.Current.CancellationToken));
    Assert.Contains("Missing subsonic-response", ex.Message);
  }

  [Fact]
  public async Task SearchSongsAsync_ReturnsSongsOnSuccess() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""searchResult3"": {
          ""song"": [
            { ""id"": ""s1"", ""title"": ""Song One"", ""artist"": ""Artist One"", ""duration"": 120 },
            { ""id"": ""s2"", ""title"": ""Song Two"", ""artist"": ""Artist Two"", ""duration"": 180 }
          ]
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.SearchSongsAsync("Song", TestContext.Current.CancellationToken);
    Assert.Equal(2, results.Count);
    Assert.Equal("Song One", results[0].Title);
    Assert.Equal("s1", results[0].Id);
  }

  [Fact]
  public async Task SearchSongsAsync_ReturnsEmptyWhenMissing() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""searchResult3"": {}
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.SearchSongsAsync("Song", TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task SearchSongsAsync_ReturnsEmptyWhenNoSearchResult3() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.SearchSongsAsync("Song", TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetSongAsync_ReturnsSongOnSuccess() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""song"": { ""id"": ""s1"", ""title"": ""Song One"", ""artist"": ""Artist One"", ""duration"": 120 }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var result = await client.GetSongAsync("s1", TestContext.Current.CancellationToken);
    Assert.Equal("Song One", result.Title);
    Assert.Equal("s1", result.Id);
  }

  [Fact]
  public async Task GetSongAsync_ThrowsIfNotFound() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var ex = await Assert.ThrowsAsync<Exception>(() => client.GetSongAsync("s1", TestContext.Current.CancellationToken));
    Assert.Contains("Song not found in response", ex.Message);
  }

  [Fact]
  public async Task GetArtistAsync_ReturnsArtistOnSuccess() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""artist"": { ""id"": ""a1"", ""name"": ""Artist One"" }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var result = await client.GetArtistAsync("a1", TestContext.Current.CancellationToken);
    Assert.Equal("Artist One", result.Name);
    Assert.Equal("a1", result.Id);
  }

  [Fact]
  public async Task GetArtistAsync_ThrowsIfNotFound() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var ex = await Assert.ThrowsAsync<Exception>(() => client.GetArtistAsync("a1", TestContext.Current.CancellationToken));
    Assert.Contains("Artist not found in response", ex.Message);
  }

  [Fact]
  public async Task GetAlbumsByArtistAsync_ReturnsAlbumsOnSuccess() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""artist"": { 
          ""id"": ""a1"", 
          ""name"": ""Artist One"",
          ""album"": [
            { ""id"": ""al1"", ""name"": ""Album One"", ""artist"": ""Artist One"" }
          ]
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var artist = new Artist("a1", "Artist One");
    var results = await client.GetAlbumsByArtistAsync(artist, TestContext.Current.CancellationToken);
    Assert.Single(results);
    Assert.Equal("Album One", results[0].Name);
    Assert.Equal("al1", results[0].Id);
  }

  [Fact]
  public async Task GetAlbumsByArtistAsync_ReturnsEmptyWhenMissing() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""artist"": { 
          ""id"": ""a1"", 
          ""name"": ""Artist One""
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var artist = new Artist("a1", "Artist One");
    var results = await client.GetAlbumsByArtistAsync(artist, TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetAlbumsByArtistAsync_ReturnsEmptyWhenNoArtist() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var artist = new Artist("a1", "Artist One");
    var results = await client.GetAlbumsByArtistAsync(artist, TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetSongsByAlbumAsync_ReturnsSongsOnSuccess() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""album"": { 
          ""id"": ""al1"", 
          ""name"": ""Album One"",
          ""song"": [
            { ""id"": ""s1"", ""title"": ""Song One"", ""artist"": ""Artist One"", ""duration"": 120 }
          ]
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var album = new Album("al1", new Artist("a1", "Artist One"), "Album One", Array.Empty<AlbumCover>());
    var results = await client.GetSongsByAlbumAsync(album, TestContext.Current.CancellationToken);
    Assert.Single(results);
    Assert.Equal("Song One", results[0].Title);
    Assert.Equal("s1", results[0].Id);
  }

  [Fact]
  public async Task GetSongsByAlbumAsync_ReturnsEmptyWhenMissing() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""album"": { 
          ""id"": ""al1"", 
          ""title"": ""Album One""
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var album = new Album("al1", new Artist("a1", "Artist One"), "Album One", Array.Empty<AlbumCover>());
    var results = await client.GetSongsByAlbumAsync(album, TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetSongsByAlbumAsync_ReturnsEmptyWhenNoAlbum() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var album = new Album("al1", new Artist("a1", "Artist One"), "Album One", Array.Empty<AlbumCover>());
    var results = await client.GetSongsByAlbumAsync(album, TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetSongStreamAsync_ReturnsStreamOnSuccess() {
    var contentBytes = new byte[] { 1, 2, 3, 4, 5 };
    var client = CreateClientWithMockStreamResponse(contentBytes);
    var stream = await client.GetSongStreamAsync("s1", TestContext.Current.CancellationToken);
    Assert.Equal("s1", stream.Id);
    Assert.Equal("mp3", stream.Codec);
    Assert.Equal(5, stream.Stream.Length);
  }

  [Fact]
  public async Task GetLikedSongsAsync_ReturnsSongsOnSuccess() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""starred"": {
          ""song"": [
            { ""id"": ""s1"", ""title"": ""Song One"", ""artist"": ""Artist One"", ""duration"": 120 }
          ]
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.GetLikedSongsAsync(TestContext.Current.CancellationToken);
    Assert.Single(results);
    Assert.Equal("Song One", results[0].Title);
    Assert.Equal("s1", results[0].Id);
  }

  [Fact]
  public async Task GetLikedSongsAsync_ReturnsEmptyWhenMissing() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""starred"": {}
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.GetLikedSongsAsync(TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }
  
  [Fact]
  public async Task GetLikedSongsAsync_ReturnsEmptyWhenNoStarred() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.GetLikedSongsAsync(TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task SearchPlaylistsAsync_ReturnsPlaylistsOnSuccess() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""playlists"": {
          ""playlist"": [
            { ""id"": ""p1"", ""name"": ""My Playlist"" },
            { ""id"": ""p2"", ""name"": ""Other"" }
          ]
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.SearchPlaylistsAsync("My", TestContext.Current.CancellationToken);
    Assert.Single(results);
    Assert.Equal("My Playlist", results[0].Name);
    Assert.Equal("p1", results[0].Id);
  }

  [Fact]
  public async Task SearchPlaylistsAsync_ReturnsEmptyWhenMissing() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""playlists"": {}
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.SearchPlaylistsAsync("My", TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task SearchPlaylistsAsync_ReturnsEmptyWhenNoPlaylists() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var results = await client.SearchPlaylistsAsync("My", TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetPlaylistSongsAsync_ReturnsSongsOnSuccess() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""playlist"": {
          ""id"": ""p1"", 
          ""name"": ""My Playlist"",
          ""entry"": [
            { ""id"": ""s1"", ""title"": ""Song One"", ""artist"": ""Artist One"", ""duration"": 120 }
          ]
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var playlist = new Playlist("p1", "My Playlist");
    var results = await client.GetPlaylistSongsAsync(playlist, TestContext.Current.CancellationToken);
    Assert.Single(results);
    Assert.Equal("Song One", results[0].Title);
    Assert.Equal("s1", results[0].Id);
  }

  [Fact]
  public async Task GetPlaylistSongsAsync_ReturnsEmptyWhenMissing() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""playlist"": {
          ""id"": ""p1"", 
          ""name"": ""My Playlist""
        }
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var playlist = new Playlist("p1", "My Playlist");
    var results = await client.GetPlaylistSongsAsync(playlist, TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetPlaylistSongsAsync_ReturnsEmptyWhenNoPlaylist() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var playlist = new Playlist("p1", "My Playlist");
    var results = await client.GetPlaylistSongsAsync(playlist, TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetPlaylistSongsFromUrlAsync_ReturnsEmptyList() {
    var client = CreateClientWithMockResponse("{}");
    var results = await client.GetPlaylistSongsFromUrlAsync("http://example.com/playlist", TestContext.Current.CancellationToken);
    Assert.Empty(results);
  }

  [Fact]
  public async Task AddToListenHistory_CallsScrobbleView() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1""
      }
    }";
    var client = CreateClientWithMockResponse(jsonResponse);
    var song = new Song("s1", new Album("al1", new Artist("a1", "Artist One"), "Album One", Array.Empty<AlbumCover>()), "Song One", TimeSpan.FromSeconds(120));
    await client.AddToListenHistory(song, TestContext.Current.CancellationToken);
    // If it doesn't throw, it successfully parsed the response
  }

  [Fact]
  public async Task GetAlbumArtAsync_ReturnsImageOnSuccess() {
    using var image = new Image<Rgba32>(10, 10);
    using var stream = new MemoryStream();
    await image.SaveAsPngAsync(stream, TestContext.Current.CancellationToken);
    var contentBytes = stream.ToArray();

    var client = CreateClientWithMockStreamResponse(contentBytes);
    var album = new Album("al1", new Artist("a1", "Artist One"), "Album One", new[] { new AlbumCover("http://localhost/cover", 10, 10) });
    var resultImage = await client.GetAlbumArtAsync(album, null, TestContext.Current.CancellationToken);
    
    Assert.NotNull(resultImage);
    Assert.Equal(10, resultImage.Width);
    Assert.Equal(10, resultImage.Height);
  }

  [Fact]
  public async Task GetAlbumArtAsync_ReturnsImageWithCoverSelector() {
    using var image = new Image<Rgba32>(10, 10);
    using var stream = new MemoryStream();
    await image.SaveAsPngAsync(stream, TestContext.Current.CancellationToken);
    var contentBytes = stream.ToArray();

    var client = CreateClientWithMockStreamResponse(contentBytes);
    var album = new Album("al1", new Artist("a1", "Artist One"), "Album One", new[] { 
      new AlbumCover("http://localhost/cover1", 10, 10),
      new AlbumCover("http://localhost/cover2", 20, 20)
    });
    var resultImage = await client.GetAlbumArtAsync(album, covers => covers.ElementAt(1), TestContext.Current.CancellationToken);
    
    Assert.NotNull(resultImage);
    Assert.Equal(10, resultImage.Width); // Stream is mocked so it just returns the 10x10 regardless of url
    Assert.Equal(10, resultImage.Height);
  }

  [Fact]
  public async Task GetAlbumArtAsync_ThrowsIfNoCoverAvailable() {
    var client = CreateClientWithMockResponse("{}");
    var album = new Album("al1", new Artist("a1", "Artist One"), "Album One", Array.Empty<AlbumCover>());
    var ex = await Assert.ThrowsAsync<Exception>(() => client.GetAlbumArtAsync(album, null, TestContext.Current.CancellationToken));
    Assert.Contains("No album cover available", ex.Message);
  }

  [Fact]
  public void Create_InitializesCorrectly_WithValidConfig() {
    SubsonicConfig.ServerHost = "example.com";
    SubsonicConfig.Username = "user";
    SubsonicConfig.Password = "pass";
    
    var client = SubsonicStreamingClient.Create(new NoCachingCacheService(), new NoCachingCacheService());
    Assert.NotNull(client);
  }

  [Fact]
  public void Create_ThrowsIfHostNotConfigured() {
    SubsonicConfig.ServerHost = null;
    SubsonicConfig.Username = "user";
    SubsonicConfig.Password = "pass";
    
    var ex = Assert.Throws<InvalidOperationException>(() => SubsonicStreamingClient.Create(new NoCachingCacheService(), new NoCachingCacheService()));
    Assert.Contains("Subsonic Server Host not configured", ex.Message);
  }

  [Fact]
  public void Create_ThrowsIfUsernameNotConfigured() {
    SubsonicConfig.ServerHost = "example.com";
    SubsonicConfig.Username = null;
    SubsonicConfig.Password = "pass";
    
    var ex = Assert.Throws<InvalidOperationException>(() => SubsonicStreamingClient.Create(new NoCachingCacheService(), new NoCachingCacheService()));
    Assert.Contains("Subsonic Username not configured", ex.Message);
  }

  [Fact]
  public void Create_ThrowsIfPasswordNotConfigured() {
    SubsonicConfig.ServerHost = "example.com";
    SubsonicConfig.Username = "user";
    SubsonicConfig.Password = null;
    
    var ex = Assert.Throws<InvalidOperationException>(() => SubsonicStreamingClient.Create(new NoCachingCacheService(), new NoCachingCacheService()));
    Assert.Contains("Subsonic Password not configured", ex.Message);
  }

  [Fact]
  public async Task Api_SendsCorrectQuery_WithoutToken() {
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""artist"": { ""id"": ""a1"", ""name"": ""Artist One"" }
      }
    }";

    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.Is<HttpRequestMessage>(req => 
          req.RequestUri!.ToString().Contains("p=pass") && 
          !req.RequestUri!.ToString().Contains("t=")
        ),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent(jsonResponse),
      });

    var httpClient = new HttpClient(handlerMock.Object);
    var client = SubsonicStreamingClient.CreateForTesting("localhost", "user", "pass", false);
    var httpClientField = typeof(SubsonicStreamingClient).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    httpClientField?.SetValue(client, httpClient);

    await client.GetArtistAsync("a1", TestContext.Current.CancellationToken);
  }
}
