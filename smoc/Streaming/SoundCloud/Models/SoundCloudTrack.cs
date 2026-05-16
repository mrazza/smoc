using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

/// <summary>
/// Represents a SoundCloud track.
/// </summary>
/// <param name="Id">The track ID.</param>
/// <param name="Title">The track title.</param>
/// <param name="Duration">The track duration in milliseconds.</param>
/// <param name="ArtworkUrl">The track artwork URL.</param>
/// <param name="User">The user who uploaded the track.</param>
/// <param name="Media">The track media info.</param>
public record SoundCloudTrack(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("duration")] long Duration,
    [property: JsonPropertyName("artwork_url")] string? ArtworkUrl,
    [property: JsonPropertyName("user")] SoundCloudUser User,
    [property: JsonPropertyName("media")] SoundCloudMedia Media
);

/// <summary>
/// Represents SoundCloud media info.
/// </summary>
/// <param name="Transcodings">The list of transcodings.</param>
public record SoundCloudMedia(
    [property: JsonPropertyName("transcodings")] List<SoundCloudTranscoding> Transcodings
);

/// <summary>
/// Represents a SoundCloud transcoding.
/// </summary>
/// <param name="Url">The transcoding URL.</param>
/// <param name="Preset">The transcoding preset.</param>
/// <param name="Format">The transcoding format.</param>
public record SoundCloudTranscoding(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("preset")] string Preset,
    [property: JsonPropertyName("format")] SoundCloudFormat Format
);

/// <summary>
/// Represents a SoundCloud format.
/// </summary>
/// <param name="Protocol">The format protocol.</param>
/// <param name="MimeType">The format MIME type.</param>
public record SoundCloudFormat(
    [property: JsonPropertyName("protocol")] string Protocol,
    [property: JsonPropertyName("mime_type")] string MimeType
);