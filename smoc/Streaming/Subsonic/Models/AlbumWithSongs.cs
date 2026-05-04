using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record AlbumWithSongs(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artist")] string ArtistName,
    [property: JsonPropertyName("artistId")] string ArtistId,
    [property: JsonPropertyName("song")] List<Song>? Songs
);