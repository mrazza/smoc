using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Represents artist information from the Tidal API.
/// </summary>
/// <param name="Id">The artist's identifier.</param>
/// <param name="Name">The artist's name.</param>
/// <param name="Picture">The artist's picture identifier.</param>
public record TidalArtist(
  [property: JsonPropertyName("id")] long Id,
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("picture")] string? Picture = null
);
