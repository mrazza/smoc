namespace Smoc.Streaming;

/// <summary>
/// Represents a playable audio stream for a song.
/// </summary>
/// <param name="Id">The ID of the song.</param>
/// <param name="Codec">The audio codec used.</param>
/// <param name="Stream">The raw audio stream.</param>
/// <param name="LoudnessDb">Optional loudness offset in dB relative to target.</param>
public sealed record SongStream(string Id, string Codec, Stream Stream, float? LoudnessDb = null);
