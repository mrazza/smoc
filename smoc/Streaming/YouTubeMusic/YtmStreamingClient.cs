using System.Net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Caching;
using Terminal.Gui.App;
using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models.Info;
using YouTubeMusicAPI.Models.Search;
using YouTubeMusicAPI.Models.Streaming;
using YouTubeSessionGenerator;
using YouTubeSessionGenerator.Js.Environments;

namespace Smoc.Streaming.YouTubeMusic;

/// <summary>
/// A streaming client implementation for YouTube Music.
/// </summary>
public sealed class YtmStreamingClient : IStreamingClient {
  private readonly YouTubeMusicClient? _authedYtmClient;
  private readonly ICacheService _songCacheService;
  private readonly ICacheService _albumArtCacheService;
  private readonly YouTubeMusicClient _ytmClient;
  private readonly HttpClient _httpClient;
  private YtmStreamingClient(YouTubeMusicClient? authedYtmClient = null, ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    _authedYtmClient = authedYtmClient;
    _songCacheService = songCacheService ?? new NoCachingCacheService();
    _albumArtCacheService = albumArtCacheService ?? new NoCachingCacheService();
    _ytmClient = new();
    _httpClient = new();
  }

  /// <inheritdoc/>
  public async Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) {
    var search = _ytmClient.SearchAsync(query, SearchCategory.Artists);
    var results = await search.FetchItemsAsync(limit: 100, cancellationToken: cancellationToken);
    return results.OfType<ArtistSearchResult>().Select(r => new Artist(r.Id, r.Name)).ToList();
  }

  /// <inheritdoc/>
  public async Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default) {
    var search = _ytmClient.SearchAsync(query, SearchCategory.Songs);
    var results = await search.FetchItemsAsync(limit: 100, cancellationToken: cancellationToken);
    return results.OfType<SongSearchResult>().Where(r => r.Album is not null && r.Artists.Length > 0).Select(
        r => new Song(
            r.Id,
            new Album(
                r.Album!.Id!,
                new Artist(r.Artists.First().Id!, r.Artists.First().Name),
                r.Album.Name,
                r.Thumbnails.Select(t => new AlbumCover(t.Url, t.Width, t.Height))),
            r.Name,
            r.Duration)).ToList();
  }

  /// <inheritdoc/>
  public async Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default) {
    var songInfo = await _ytmClient.GetSongVideoInfoAsync(songId, cancellationToken);
    var albumInfo = await _ytmClient.GetAlbumInfoAsync(songInfo.Album!.Id!, cancellationToken);
    return new Song(
        songInfo.Id,
        new Album(
            albumInfo.Id,
            new Artist(albumInfo.Artists.First().Id!, albumInfo.Artists.First().Name),
            albumInfo.Name,
            albumInfo.Thumbnails.Select(t => new AlbumCover(t.Url, t.Width, t.Height)),
            albumInfo.ReleaseYear),
        songInfo.Name,
        songInfo.Duration);
  }

  /// <inheritdoc/>
  public async Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default) {
    var result = await _ytmClient.GetArtistInfoAsync(artistId, cancellationToken);
    return new Artist(result.Id, result.Name);
  }

  /// <inheritdoc/>
  public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default) {
    var results = await _ytmClient.GetArtistInfoAsync(artist.Id, cancellationToken);
    return results.Albums.Select(s => new Album(
        s.Id,
        artist,
        s.Name,
        s.Thumbnails.Select(t => new AlbumCover(t.Url, t.Width, t.Height)),
        s.ReleaseYear)).ToList();
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default) {
    var results = await _ytmClient.GetAlbumInfoAsync(album.Id, cancellationToken);
    return results.Songs.Select(s => new Song(s.Id!, album, s.Name, s.Duration, s.SongNumber)).ToList();
  }

  /// <inheritdoc/>
  public async Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default) {
    if (_authedYtmClient == null)
      throw new InvalidOperationException("No authed YTM client privided.");

    var streamingData = await _authedYtmClient.GetStreamingDataAsync(songId, cancellationToken);
    var highestAudioStreamInfo = streamingData.StreamInfo
        .OfType<AudioStreamInfo>()
        .OrderByDescending(info => info.Bitrate)
        .First();
    var stream = await _songCacheService.GetOrAddAsync(
      string.Concat(songId, "-", highestAudioStreamInfo.Bitrate.ToString()),
      highestAudioStreamInfo.GetStreamAsync,
      cancellationToken);
    return new SongStream(songId, highestAudioStreamInfo.Container.Codecs, stream, (float?)highestAudioStreamInfo.LoudnessDb);
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default) {
    if (_authedYtmClient is null)
      throw new InvalidOperationException("No authed YTM client provided.");

    var result = _authedYtmClient.GetCommunityPlaylistSongsAsync(_authedYtmClient.GetCommunityPlaylistBrowseId("LM"));
    var results = await result.FetchItemsAsync(limit: 1000, cancellationToken: cancellationToken);
    return results.OfType<CommunityPlaylistSong>().Select(s =>
        new Song(
          s.Id,
          new Album(
            s.Album!.Id!,
            new Artist(s.Artists.First().Id!, s.Artists.First().Name),
            s.Album.Name,
            s.Thumbnails.Select(t => new AlbumCover(t.Url, t.Width, t.Height))),
          s.Name,
          s.Duration)).ToList();
  }

  /// <inheritdoc/>
  public async Task<List<Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default) {
    var queryWords = query.Split(' ');
    var search = _ytmClient.SearchAsync(query, SearchCategory.CommunityPlaylists);
    var libraryPlaylistsTask = _authedYtmClient?.GetLibraryCommunityPlaylistsAsync(cancellationToken: cancellationToken);
    var searchResultsTask = search.FetchItemsAsync(limit: 100, cancellationToken: cancellationToken);
    await Task.WhenAll(searchResultsTask, libraryPlaylistsTask ?? Task.CompletedTask);
    var libraryPlaylists = libraryPlaylistsTask?.Result
      .Where(p => queryWords.All(word => p.Name.Contains(word, StringComparison.InvariantCultureIgnoreCase)))
      .Select(p => new Playlist(p.Id, p.Name)).ToList() ?? [];
    var searchResults = searchResultsTask.Result.OfType<CommunityPlaylistSearchResult>()
      .Where(p => !libraryPlaylists.Any(l => l.Id == p.Id))
      .Select(r => new Playlist(r.Id, r.Name));
    return [.. libraryPlaylists, .. searchResults];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetPlaylistSongsAsync(Playlist playlist, CancellationToken cancellationToken = default) {
    if (_authedYtmClient is null)
      throw new InvalidOperationException("No authed YTM client provided.");

    var result = _authedYtmClient.GetCommunityPlaylistSongsAsync(_authedYtmClient.GetCommunityPlaylistBrowseId(playlist.Id));
    var results = await result.FetchItemsAsync(limit: 1000, cancellationToken: cancellationToken);
    return results.OfType<CommunityPlaylistSong>().Where(r => r.Album is not null && r.Artists.Length > 0).Select(s =>
        new Song(
          s.Id,
          new Album(
            s.Album!.Id!,
            new Artist(s.Artists.First().Id!, s.Artists.First().Name),
            s.Album.Name,
            s.Thumbnails.Select(t => new AlbumCover(t.Url, t.Width, t.Height))),
          s.Name,
          s.Duration)).ToList();
  }

  /// <inheritdoc/>
  public async Task AddToListenHistory(Song song, CancellationToken cancellationToken = default) {
    if (_authedYtmClient is null)
      throw new InvalidOperationException("No authed YTM client provided.");

    await _authedYtmClient.AddToWatchHistoryAsync(await _authedYtmClient.GetSongVideoInfoAsync(song.Id, cancellationToken), cancellationToken);
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default) {
    if (_authedYtmClient is null)
      throw new InvalidOperationException("No authed YTM client provided.");

    var (entityType, id) = YtmUrlParser.ParseUrl(url);

    return entityType switch {
      _ when entityType == typeof(Song) => [await GetSongAsync(id, cancellationToken)],
      _ when entityType == typeof(Playlist) => await GetPlaylistSongsAsync(new Playlist(id, ""), cancellationToken),
      _ => throw new ArgumentException("Invalid URL.", nameof(url))
    };
  }

  /// <inheritdoc/>
  public async Task<Image<Rgba32>> GetAlbumArtAsync(Album album, Func<IEnumerable<AlbumCover>, AlbumCover>? coverSelector = null, CancellationToken cancellationToken = default) {
    if (!album.Covers.Any())
      throw new ArgumentException("Album has no covers.", nameof(album));

    var cover = coverSelector?.Invoke(album.Covers) ?? album.Covers.First();

    using var albumArt = await _albumArtCacheService.GetOrAddAsync(
      string.Concat(album.Id, "-", cover.Width, "x", cover.Height),
      async ct => {
        var albumResponse = await _httpClient.GetAsync(cover.Url, cancellationToken);
        return await albumResponse.Content.ReadAsStreamAsync(cancellationToken);
      },
      cancellationToken);
    return await Image.LoadAsync<Rgba32>(albumArt, cancellationToken);
  }

  /// <summary>
  /// Parses cookies from the specified cookie file.
  /// </summary>
  /// <param name="file">The path to the cookie file.</param>
  /// <returns>A list of parsed cookies.</returns>
  public static List<Cookie> GetCookiesFromFile(string file) {
    string cookieString = File.ReadAllText(file);
    return cookieString.Split(";").Select(x => x.Trim().Split("=")).Select(x => new Cookie(x[0], x[1], "", "music.youtube.com")).ToList();
  }

  /// <summary>
  /// Generates the necessary tokens (Proof of Origin, Rollout, Visitor) for authenticated requests.
  /// </summary>
  /// <param name="cookies">The user's cookies.</param>
  /// <returns>The generated tokens.</returns>
  public static async Task<YtmTokens> GenerateTokensAsync(List<Cookie> cookies) {
    using NodeEnvironment myCustomJsEnvironment = new();
    var cookieContainer = new CookieContainer();
    foreach (var cookie in cookies)
      cookieContainer.Add(cookie);
    using HttpClient httpClient = new(new HttpClientHandler() { CookieContainer = cookieContainer });

    YouTubeSessionConfig config = new() {
      JsEnvironment = myCustomJsEnvironment,  // Required when generating Proof of Origin Tokens
      HttpClient = httpClient,
    };
    YouTubeSessionCreator creator = new(config);

    string visitorData = await creator.VisitorDataAsync();
    string poToken = await creator.ProofOfOriginTokenAsync(visitorData);
    string rolloutToken = await creator.RolloutTokenAsync();

    return new YtmTokens(poToken, rolloutToken, visitorData);
  }

  /// <summary>
  /// Creates an authenticated YouTube Music client.
  /// </summary>
  /// <param name="cookies">The user's cookies.</param>
  /// <param name="tokens">The generated tokens.</param>
  /// <param name="cacheService">Optional cache service to use for caching streams; if not provided, no caching will be used.</param>
  /// <returns>An initialized client.</returns>
  public static YtmStreamingClient Create(List<Cookie> cookies, YtmTokens tokens, ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    return new YtmStreamingClient(new(logger: Logging.Logger, cookies: cookies, poToken: tokens.PoToken, visitorData: tokens.VisitorData, playerId: YouTubeMusicConfig.Defaults.PlayerId), songCacheService, albumArtCacheService);
  }

  /// <summary>
  /// Creates an unauthenticated YouTube Music client.
  /// </summary>
  /// <param name="cacheService">Optional cache service to use for caching streams; if not provided, no caching will be used.</param>
  /// <returns>An initialized client.</returns>
  public static YtmStreamingClient Create(ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    return new YtmStreamingClient(songCacheService: songCacheService, albumArtCacheService: albumArtCacheService);
  }
}
