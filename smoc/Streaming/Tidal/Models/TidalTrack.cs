using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Track metadata from the Tidal API.
/// </summary>
/// <param name="Id">The unique track identifier.</param>
/// <param name="Title">The title of the track.</param>
/// <param name="Duration">The duration of the track in seconds.</param>
/// <param name="TrackNumber">The track number on its album.</param>
/// <param name="Album">The album the track belongs to.</param>
/// <param name="Artist">The primary artist of the track.</param>
/// <param name="Artists">The list of contributing artists.</param>
public record TidalTrack(
  [property: JsonPropertyName("id")] long Id,
  [property: JsonPropertyName("title")] string Title,
  [property: JsonPropertyName("duration")] int Duration,
  [property: JsonPropertyName("trackNumber")] int TrackNumber,
  [property: JsonPropertyName("album")] TidalAlbum? Album = null,
  [property: JsonPropertyName("artist")] TidalArtist? Artist = null,
  [property: JsonPropertyName("artists")] List<TidalArtist>? Artists = null
);
