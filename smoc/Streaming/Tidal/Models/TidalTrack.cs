using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalTrack(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("duration")] int Duration,
    [property: JsonPropertyName("trackNumber")] int TrackNumber,
    [property: JsonPropertyName("album")] TidalAlbum Album,
    [property: JsonPropertyName("artist")] TidalArtist Artist,
    [property: JsonPropertyName("artists")] List<TidalArtist> Artists
);