using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic;

public record SubsonicResponse<T>(
    [property: JsonPropertyName("subsonic-response")] T Response
);

public record SubsonicStatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("error")] SubsonicError? Error
);

public record SubsonicError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message
);

public record SearchResult3(
    [property: JsonPropertyName("artist")] List<ArtistDto>? Artists,
    [property: JsonPropertyName("album")] List<AlbumDto>? Albums,
    [property: JsonPropertyName("song")] List<SongDto>? Songs
);

public record ArtistDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("albumCount")] int? AlbumCount
);

public record AlbumDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artist")] string? Artist,
    [property: JsonPropertyName("artistId")] string? ArtistId,
    [property: JsonPropertyName("songCount")] int? SongCount,
    [property: JsonPropertyName("duration")] int? Duration,
    [property: JsonPropertyName("coverArt")] string? CoverArt
);

public record SongDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("album")] string? Album,
    [property: JsonPropertyName("artist")] string? Artist,
    [property: JsonPropertyName("artistId")] string? ArtistId,
    [property: JsonPropertyName("albumId")] string? AlbumId,
    [property: JsonPropertyName("duration")] int? Duration,
    [property: JsonPropertyName("track")] int? Track,
    [property: JsonPropertyName("coverArt")] string? CoverArt
);

public record ArtistWithAlbumsDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("album")] List<AlbumDto>? Albums
);

public record AlbumWithSongsDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("artistId")] string ArtistId,
    [property: JsonPropertyName("song")] List<SongDto>? Songs
);

public record PlaylistDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("songCount")] int? SongCount,
    [property: JsonPropertyName("duration")] int? Duration
);

public record PlaylistWithSongsDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("entry")] List<SongDto>? Songs
);
