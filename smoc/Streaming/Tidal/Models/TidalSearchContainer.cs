using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalSearchContainer(
    [property: JsonPropertyName("artists")] TidalSearchResponse<TidalArtist>? Artists = null,
    [property: JsonPropertyName("albums")] TidalSearchResponse<TidalAlbum>? Albums = null,
    [property: JsonPropertyName("tracks")] TidalSearchResponse<TidalTrack>? Tracks = null,
    [property: JsonPropertyName("playlists")] TidalSearchResponse<TidalPlaylist>? Playlists = null
);