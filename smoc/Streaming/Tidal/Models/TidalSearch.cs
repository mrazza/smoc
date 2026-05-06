using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalSearchResponse<T>(
    [property: JsonPropertyName("items")] List<T> Items,
    [property: JsonPropertyName("totalNumberOfItems")] int TotalNumberOfItems
);

public record TidalSearchContainer(
    [property: JsonPropertyName("artists")] TidalSearchResponse<TidalArtist>? Artists,
    [property: JsonPropertyName("albums")] TidalSearchResponse<TidalAlbum>? Albums,
    [property: JsonPropertyName("tracks")] TidalSearchResponse<TidalTrack>? Tracks,
    [property: JsonPropertyName("playlists")] TidalSearchResponse<TidalPlaylist>? Playlists
);