using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Structs;
using Terminal.Gui.App;

namespace Smoc.Services;

/// <summary>
/// Internal service responsible for managing the playback of a single audio stream.
/// </summary>
internal sealed class StreamPlaybackService : IDisposable {
  private readonly MiniAudioEngine _audioEngine;
  private readonly Stream _songStream;
  private readonly AudioFormat _audioFormat;
  private readonly AudioPlaybackDevice _playbackDevice;
  private readonly AssetDataProvider _streamDataProvider;
  private readonly SoundPlayer _soundPlayer;

  /// <summary>
  /// Occurs when the stream reaches its end.
  /// </summary>
  public event EventHandler? StreamEnded;

  /// <summary>
  /// Occurs when the playback position changes.
  /// </summary>
  public event EventHandler<TimeSpan>? PositionChanged;

  /// <summary>
  /// Gets the total duration of the stream.
  /// </summary>
  public TimeSpan Duration => TimeSpan.FromSeconds(_soundPlayer.Duration);

  /// <summary>
  /// Gets the current playback time.
  /// </summary>
  public TimeSpan Time => TimeSpan.FromSeconds(_soundPlayer.Time);

  /// <summary>
  /// Gets the playback progress as a normalized value between 0.0 and 1.0.
  /// </summary>
  public float Progress => _soundPlayer.Time / _soundPlayer.Duration;

  public StreamPlaybackService(MiniAudioEngine audioEngine, AudioPlaybackDevice playbackDevice, Stream songStream, AudioFormat audioFormat) {
    _audioEngine = audioEngine;
    _playbackDevice = playbackDevice;
    _songStream = songStream;
    _audioFormat = audioFormat;

    _streamDataProvider = new AssetDataProvider(audioEngine, audioFormat, songStream);
    _soundPlayer = new SoundPlayer(audioEngine, audioFormat, _streamDataProvider);
    _playbackDevice.MasterMixer.AddComponent(_soundPlayer);
    _streamDataProvider.PositionChanged += (sender, args) => PositionChanged?.Invoke(this, Time);
    _soundPlayer.PlaybackEnded += (sender, args) => StreamEnded?.Invoke(this, EventArgs.Empty);
  }

  public void Play() {
    Logging.Debug("Playing stream...");
    _soundPlayer.Play();
  }

  public void Pause() {
    Logging.Debug("Pausing stream...");
    _soundPlayer.Pause();
  }

  public void Stop() {
    Logging.Debug("Stopping stream...");
    _soundPlayer.Stop();
  }

  public void Dispose() {
    Logging.Debug("Disposing stream...");
    _playbackDevice.MasterMixer.RemoveComponent(_soundPlayer);
    _soundPlayer.Dispose();
    _streamDataProvider.Dispose();
    _songStream.Dispose();
  }
}
