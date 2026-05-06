using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalDeviceAuthResponse(
    [property: JsonPropertyName("deviceCode")] string DeviceCode,
    [property: JsonPropertyName("userCode")] string UserCode,
    [property: JsonPropertyName("verificationUri")] string VerificationUri,
    [property: JsonPropertyName("verificationUriComplete")] string VerificationUriComplete,
    [property: JsonPropertyName("expiresIn")] int ExpiresIn,
    [property: JsonPropertyName("interval")] int Interval
);