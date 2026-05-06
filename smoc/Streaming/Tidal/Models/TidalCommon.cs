using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalArtist(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("picture")] string? Picture
);

public record TidalAlbum(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("cover")] string? Cover,
    [property: JsonPropertyName("releaseDate")] string? ReleaseDate,
    [property: JsonPropertyName("artist")] TidalArtist? Artist
);

public record TidalTrack(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("duration")] int Duration,
    [property: JsonPropertyName("trackNumber")] int TrackNumber,
    [property: JsonPropertyName("album")] TidalAlbum Album,
    [property: JsonPropertyName("artist")] TidalArtist Artist,
    [property: JsonPropertyName("artists")] List<TidalArtist> Artists
);

public record TidalPlaylist(
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description
);