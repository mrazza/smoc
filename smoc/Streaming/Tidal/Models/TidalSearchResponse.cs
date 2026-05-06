using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalSearchResponse<T>(
    [property: JsonPropertyName("items")] List<T> Items,
    [property: JsonPropertyName("totalNumberOfItems")] int TotalNumberOfItems = 0
);