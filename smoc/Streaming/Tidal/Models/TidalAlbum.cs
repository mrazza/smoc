using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalAlbum(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("cover")] string? Cover = null,
    [property: JsonPropertyName("releaseDate")] string? ReleaseDate = null,
    [property: JsonPropertyName("artist")] TidalArtist? Artist = null
);