using System.Net;
using YouTubeMusicAPI.Client;
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
  private readonly YouTubeMusicClient _ytmClient;
  private YtmStreamingClient(YouTubeMusicClient? authedYtmClient = null) {
    _authedYtmClient = authedYtmClient;
    _ytmClient = new();
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
                ThumbnailUrl: r.Thumbnails.OrderBy(t => t.Height).Select(t => t.Url).FirstOrDefault()),
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
            albumInfo.ReleaseYear,
            albumInfo.Thumbnails.OrderBy(t => t.Height).Select(t => t.Url).FirstOrDefault()),
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
        s.ReleaseYear,
        s.Thumbnails.OrderBy(t => t.Height).Select(t => t.Url).FirstOrDefault())).ToList();
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
    var stream = await highestAudioStreamInfo.GetStreamAsync(cancellationToken: cancellationToken);
    return new SongStream(songId, highestAudioStreamInfo.Container.Codecs, stream);
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
  /// <returns>An initialized client.</returns>
  public static YtmStreamingClient Create(List<Cookie> cookies, YtmTokens tokens) {
    return new YtmStreamingClient(new(cookies: cookies, poToken: tokens.PoToken, visitorData: tokens.VisitorData));
  }

  /// <summary>
  /// Creates an unauthenticated YouTube Music client.
  /// </summary>
  /// <returns>An initialized client.</returns>
  public static YtmStreamingClient Create() {
    return new YtmStreamingClient();
  }
}
