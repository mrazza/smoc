using Smoc.Streaming;

namespace Smoc.Services.Audio;

/// <summary>
/// Service for managing audio playback on the sound device.
/// </summary>
public interface IAudioService : IDisposable {

  /// <summary>
  /// Occurs when the active audio playback device changes.
  /// </summary>
  event EventHandler<AudioDeviceInfo?>? PlaybackDeviceChanged {
    add { }
    remove { }
  }

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

  /// <summary>
  /// Gets the list of available audio playback devices.
  /// </summary>
  /// <returns>A read-only list of available <see cref="AudioDeviceInfo"/> records.</returns>
  IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices() => [];

  /// <summary>
  /// Switches audio output to the device matching the specified index or name.
  /// </summary>
  /// <param name="deviceIndexOrName">The 0-based device index or partial/full device name.</param>
  /// <returns><c>true</c> if the device was found and selected; otherwise, <c>false</c>.</returns>
  bool SelectPlaybackDevice(string deviceIndexOrName) => false;

  /// <summary>
  /// Gets the currently active playback device, or <c>null</c> if none is initialized.
  /// </summary>
  AudioDeviceInfo? ActivePlaybackDevice => null;
}
