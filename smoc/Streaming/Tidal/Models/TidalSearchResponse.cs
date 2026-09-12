using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Generic paginated search or list response from the Tidal API.
/// </summary>
/// <typeparam name="T">The type of items contained in the response.</typeparam>
/// <param name="Items">List of items returned.</param>
/// <param name="TotalNumberOfItems">Total number of available items.</param>
public record TidalSearchResponse<T>(
  [property: JsonPropertyName("items")] List<T> Items,
  [property: JsonPropertyName("totalNumberOfItems")] int TotalNumberOfItems = 0
);
