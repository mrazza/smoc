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

public record TidalTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType
);