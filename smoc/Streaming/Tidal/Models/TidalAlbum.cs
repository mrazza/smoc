using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Represents album metadata from the Tidal API.
/// </summary>
/// <param name="Id">The unique identifier of the album.</param>
/// <param name="Title">The title of the album.</param>
/// <param name="Cover">The album cover image UUID.</param>
/// <param name="ReleaseDate">The release date of the album.</param>
/// <param name="Artist">The primary artist of the album.</param>
public record TidalAlbum(
  [property: JsonPropertyName("id")] long Id,
  [property: JsonPropertyName("title")] string Title,
  [property: JsonPropertyName("cover")] string? Cover = null,
  [property: JsonPropertyName("releaseDate")] string? ReleaseDate = null,
  [property: JsonPropertyName("artist")] TidalArtist? Artist = null
);
