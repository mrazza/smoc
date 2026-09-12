using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Response payload for Tidal OAuth device authorization request.
/// </summary>
/// <param name="DeviceCode">The device verification code.</param>
/// <param name="UserCode">The end-user verification code to enter.</param>
/// <param name="VerificationUri">The URL where the end-user inputs the code.</param>
/// <param name="VerificationUriComplete">The complete URL with user code embedded.</param>
/// <param name="ExpiresIn">Lifetime in seconds of device_code and user_code.</param>
/// <param name="Interval">The minimum interval in seconds to poll for token.</param>
public record TidalDeviceAuthResponse(
  [property: JsonPropertyName("deviceCode")] string DeviceCode,
  [property: JsonPropertyName("userCode")] string UserCode,
  [property: JsonPropertyName("verificationUri")] string VerificationUri,
  [property: JsonPropertyName("verificationUriComplete")] string VerificationUriComplete,
  [property: JsonPropertyName("expiresIn")] int ExpiresIn,
  [property: JsonPropertyName("interval")] int Interval
);
