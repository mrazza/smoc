using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

public record SoundCloudTrack(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("duration")] long Duration,
    [property: JsonPropertyName("artwork_url")] string? ArtworkUrl,
    [property: JsonPropertyName("user")] SoundCloudUser User,
    [property: JsonPropertyName("media")] SoundCloudMedia Media
);

public record SoundCloudMedia(
    [property: JsonPropertyName("transcodings")] List<SoundCloudTranscoding> Transcodings
);

public record SoundCloudTranscoding(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("preset")] string Preset,
    [property: JsonPropertyName("format")] SoundCloudFormat Format
);

public record SoundCloudFormat(
    [property: JsonPropertyName("protocol")] string Protocol,
    [property: JsonPropertyName("mime_type")] string MimeType
);