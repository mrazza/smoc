using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Playback stream info response from the Tidal API.
/// </summary>
/// <param name="TrackId">The track identifier.</param>
/// <param name="AssetPresentation">Asset presentation mode (e.g. FULL).</param>
/// <param name="AudioQuality">Audio quality level (e.g. LOSSLESS, HIGH).</param>
/// <param name="ManifestMimeType">The MIME type of the playback manifest.</param>
/// <param name="Manifest">Base64 encoded manifest data.</param>
public record TidalPlaybackInfo(
  [property: JsonPropertyName("trackId")] long TrackId,
  [property: JsonPropertyName("assetPresentation")] string AssetPresentation,
  [property: JsonPropertyName("audioQuality")] string AudioQuality,
  [property: JsonPropertyName("manifestMimeType")] string ManifestMimeType,
  [property: JsonPropertyName("manifest")] string Manifest
);
