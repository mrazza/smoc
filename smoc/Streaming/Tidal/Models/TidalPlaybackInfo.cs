using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalPlaybackInfo(
    [property: JsonPropertyName("trackId")] long TrackId,
    [property: JsonPropertyName("assetPresentation")] string AssetPresentation,
    [property: JsonPropertyName("audioQuality")] string AudioQuality,
    [property: JsonPropertyName("manifestMimeType")] string ManifestMimeType,
    [property: JsonPropertyName("manifest")] string Manifest
);