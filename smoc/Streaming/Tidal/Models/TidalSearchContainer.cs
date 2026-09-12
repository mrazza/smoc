using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

/// <summary>
/// Container for Tidal search query results across artists, albums, tracks, and playlists.
/// </summary>
/// <param name="Artists">Search results for artists.</param>
/// <param name="Albums">Search results for albums.</param>
/// <param name="Tracks">Search results for tracks.</param>
/// <param name="Playlists">Search results for playlists.</param>
public record TidalSearchContainer(
  [property: JsonPropertyName("artists")] TidalSearchResponse<TidalArtist>? Artists = null,
  [property: JsonPropertyName("albums")] TidalSearchResponse<TidalAlbum>? Albums = null,
  [property: JsonPropertyName("tracks")] TidalSearchResponse<TidalTrack>? Tracks = null,
  [property: JsonPropertyName("playlists")] TidalSearchResponse<TidalPlaylist>? Playlists = null
);
