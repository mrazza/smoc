using System.Linq;
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
        return results.Select(r => new Artist(r.Id, r.Name)).ToList();
    }

    public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist)
    {
        var results = await ytmClient.GetArtistInfoAsync(artist.Id);
        return results.Albums.Select(s => new Album(s.Id, artist, s.Name, s.ReleaseYear)).ToList();
    }

    public async Task<List<Song>> GetSongsByAlbumAsync(Album album)
    {
        var results = await ytmClient.GetAlbumInfoAsync(album.Id);
        return results.Songs.Select(s => new Song(s.Id!, album, s.SongNumber ?? 0, s.Name, s.Duration)).ToList();
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