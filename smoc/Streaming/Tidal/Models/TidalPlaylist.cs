using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Playlist metadata from the Tidal API.
/// </summary>
/// <param name="Uuid">Unique playlist UUID.</param>
/// <param name="Title">Title of the playlist.</param>
/// <param name="Description">Optional description of the playlist.</param>
public record TidalPlaylist(
  [property: JsonPropertyName("uuid")] string Uuid,
  [property: JsonPropertyName("title")] string Title,
  [property: JsonPropertyName("description")] string? Description = null
);
