using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// OAuth token response from the Tidal authentication endpoint.
/// </summary>
/// <param name="AccessToken">The OAuth bearer access token.</param>
/// <param name="RefreshToken">The OAuth refresh token, if provided.</param>
/// <param name="ExpiresIn">Lifetime of the access token in seconds.</param>
/// <param name="TokenType">The type of the token (typically "Bearer").</param>
public record TidalTokenResponse(
  [property: JsonPropertyName("access_token")] string AccessToken,
  [property: JsonPropertyName("refresh_token")] string? RefreshToken = null,
  [property: JsonPropertyName("expires_in")] int ExpiresIn = 0,
  [property: JsonPropertyName("token_type")] string TokenType = "Bearer"
);
