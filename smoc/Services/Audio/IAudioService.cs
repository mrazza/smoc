using Smoc.Streaming;

namespace Smoc.Services.Audio;

/// <summary>
/// Service for managing audio playback on the sound device.
/// </summary>
public interface IAudioService : IDisposable {

  /// <summary>
  /// Gets or sets the master volume (0.0 to 2.0).
  /// </summary>
  float Volume { get; set; }

  /// <summary>
  /// Creates a new playback service for the given stream and codec.
  /// </summary>
  IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, CancellationToken cancellationToken = default) =>
    MakePlaybackService(song, stream, codec, null, cancellationToken);

  /// <summary>
  /// Creates a new playback service for the given stream, codec, and optional loudness metadata.
  /// </summary>
  IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, float? loudnessDb, CancellationToken cancellationToken = default);
}
