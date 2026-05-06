using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalManifest(
    [property: JsonPropertyName("mimeType")] string MimeType,
    [property: JsonPropertyName("codecs")] string Codecs,
    [property: JsonPropertyName("encryptionType")] string EncryptionType,
    [property: JsonPropertyName("urls")] List<string> Urls
);