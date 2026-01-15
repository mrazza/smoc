using System.Net;
using Terminal.Gui.App;
using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models.Search;
using YouTubeMusicAPI.Models.Streaming;
using YouTubeSessionGenerator;
using YouTubeSessionGenerator.Js.Environments;

namespace Smoc.Streaming.YouTubeMusic;

public sealed class YtmStreamingClient : IStreamingClient
{
    private readonly YouTubeMusicClient? authedYtmClient;
    private readonly YouTubeMusicClient ytmClient;
    private YtmStreamingClient(YouTubeMusicClient? authedYtmClient = null)
    {
        this.authedYtmClient = authedYtmClient;
        this.ytmClient = new();
    }

    public async Task<List<Artist>> SearchArtistsAsync(string query)
    {
        var search = ytmClient.SearchAsync(query, SearchCategory.Artists);
        var results = await search.FetchItemsAsync(limit: 100);
        return results.OfType<ArtistSearchResult>().Select(r => new Artist(r.Id, r.Name)).ToList();
    }

    public async Task<List<Song>> SearchSongsAsync(string query)
    {
        var search = ytmClient.SearchAsync(query, SearchCategory.Songs);
        var results = await search.FetchItemsAsync(limit: 100);
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

    public async Task<Song> GetSongAsync(string songId)
    {
        var songInfo = await ytmClient.GetSongVideoInfoAsync(songId);
        var albumInfo = await ytmClient.GetAlbumInfoAsync(songInfo.Album!.Id!);
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

    public async Task<Artist> GetArtistAsync(string artistId)
    {
        var result = await ytmClient.GetArtistInfoAsync(artistId);
        return new Artist(result.Id, result.Name);
    }

    public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist)
    {
        var results = await ytmClient.GetArtistInfoAsync(artist.Id);
        return results.Albums.Select(s => new Album(
            s.Id,
            artist,
            s.Name,
            s.ReleaseYear,
            s.Thumbnails.OrderBy(t => t.Height).Select(t => t.Url).FirstOrDefault())).ToList();
    }

    public async Task<List<Song>> GetSongsByAlbumAsync(Album album)
    {
        var results = await ytmClient.GetAlbumInfoAsync(album.Id);
        return results.Songs.Select(s => new Song(s.Id!, album, s.Name, s.Duration, s.SongNumber)).ToList();
    }

    public async Task<SongStream> GetSongStreamAsync(string songId)
    {
        if (authedYtmClient == null)
            throw new InvalidOperationException("No authed YTM client privided.");

        var streamingData = await authedYtmClient.GetStreamingDataAsync(songId);
        var highestAudioStreamInfo = streamingData.StreamInfo
            .OfType<AudioStreamInfo>()
            .OrderByDescending(info => info.Bitrate)
            .First();
        var stream = await highestAudioStreamInfo.GetStreamAsync();
        return new SongStream(songId, highestAudioStreamInfo.Container.Codecs, stream);
    }

    public static List<Cookie> GetCookiesFromFile(string file)
    {
        string cookieString = File.ReadAllText(file);
        return cookieString.Split(";").Select(x => x.Trim().Split("=")).Select(x => new Cookie(x[0], x[1], "", "music.youtube.com")).ToList();
    }

    public static async Task<YtmTokens> GenerateTokensAsync(List<Cookie> cookies)
    {
        using NodeEnvironment myCustomJsEnvironment = new();
        var cookieContainer = new CookieContainer();
        foreach (var cookie in cookies)
            cookieContainer.Add(cookie);
        using HttpClient httpClient = new(new HttpClientHandler() { CookieContainer = cookieContainer });

        YouTubeSessionConfig config = new()
        {
            JsEnvironment = myCustomJsEnvironment,  // Required when generating Proof of Origin Tokens
            HttpClient = httpClient,
        };
        YouTubeSessionCreator creator = new(config);

        string visitorData = await creator.VisitorDataAsync();
        string poToken = await creator.ProofOfOriginTokenAsync(visitorData);
        string rolloutToken = await creator.RolloutTokenAsync();

        return new YtmTokens(poToken, rolloutToken, visitorData);
    }

    public static YtmStreamingClient Create(List<Cookie> cookies, YtmTokens tokens)
    {
        return new YtmStreamingClient(new(cookies: cookies, poToken: tokens.PoToken, visitorData: tokens.VisitorData));
    }

    public static YtmStreamingClient Create()
    {
        return new YtmStreamingClient();
    }
}