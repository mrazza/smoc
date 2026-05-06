using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalPlaybackInfo(
    [property: JsonPropertyName("trackId")] long TrackId,
    [property: JsonPropertyName("assetPresentation")] string AssetPresentation,
    [property: JsonPropertyName("audioQuality")] string AudioQuality,
    [property: JsonPropertyName("manifestMimeType")] string ManifestMimeType,
    [property: JsonPropertyName("manifest")] string Manifest
);

public record TidalManifest(
    [property: JsonPropertyName("mimeType")] string MimeType,
    [property: JsonPropertyName("codecs")] string Codecs,
    [property: JsonPropertyName("encryptionType")] string EncryptionType,
    [property: JsonPropertyName("urls")] List<string> Urls
);