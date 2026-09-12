using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Playback manifest extracted from Tidal playback info.
/// </summary>
/// <param name="MimeType">MIME type of the media stream.</param>
/// <param name="Codecs">Audio codec identifier.</param>
/// <param name="EncryptionType">Type of encryption used, if any.</param>
/// <param name="Urls">List of stream URLs.</param>
public record TidalManifest(
  [property: JsonPropertyName("mimeType")] string MimeType,
  [property: JsonPropertyName("codecs")] string Codecs,
  [property: JsonPropertyName("encryptionType")] string EncryptionType,
  [property: JsonPropertyName("urls")] List<string> Urls
);
