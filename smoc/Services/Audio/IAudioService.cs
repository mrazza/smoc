using Smoc.Streaming;

namespace Smoc.Services.Audio;

/// <summary>
/// Service for managing audio playback on the sound device.
/// </summary>
public interface IAudioService : IDisposable {

  /// <summary>
  /// Occurs when the active audio playback device changes.
  /// </summary>
  event EventHandler<AudioDeviceInfo?>? PlaybackDeviceChanged;

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
  IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();

  /// <summary>
  /// Switches audio output to the device matching the specified index.
  /// </summary>
  /// <param name="deviceIndex">The 0-based index of the audio device.</param>
  /// <returns><c>true</c> if the device was selected; otherwise, <c>false</c>.</returns>
  bool SelectPlaybackDevice(int deviceIndex);

  /// <summary>
  /// Switches audio output to the specified audio device.
  /// </summary>
  /// <param name="device">The audio device to select.</param>
  /// <returns><c>true</c> if the device was selected; otherwise, <c>false</c>.</returns>
  bool SelectPlaybackDevice(AudioDeviceInfo device) => SelectPlaybackDevice(device.Index);

  /// <summary>
  /// Gets the currently active playback device, or <c>null</c> if none is initialized.
  /// </summary>
  AudioDeviceInfo? ActivePlaybackDevice { get; }
}
