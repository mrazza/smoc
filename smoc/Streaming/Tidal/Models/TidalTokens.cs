using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Saved authentication tokens and credentials for Tidal.
/// </summary>
/// <param name="AccessToken">The OAuth access token.</param>
/// <param name="RefreshToken">The OAuth refresh token.</param>
/// <param name="TokenExpiry">The expiration timestamp in epoch seconds.</param>
/// <param name="ClientId">The client ID associated with the tokens.</param>
public record TidalTokens(
  [property: JsonPropertyName("access_token")] string? AccessToken,
  [property: JsonPropertyName("refresh_token")] string? RefreshToken,
  [property: JsonPropertyName("token_expiry")] long? TokenExpiry,
  [property: JsonPropertyName("client_id")] string? ClientId
);
