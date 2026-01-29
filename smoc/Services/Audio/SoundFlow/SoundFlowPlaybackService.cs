using System.Diagnostics;
using Smoc.Streaming;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Structs;
using Terminal.Gui.App;
using SoundFlowPlaybackState = SoundFlow.Enums.PlaybackState;

namespace Smoc.Services.Audio.SoundFlow;

/// <summary>
/// Implementation of <see cref="IPlaybackService"/> using SoundFlow.
/// </summary>  
public sealed class SoundFlowPlaybackService : IPlaybackService {
  private readonly AudioPlaybackDevice _playbackDevice;
  private readonly Stream _songStream;
  private readonly AudioFormat _audioFormat;
  private readonly AssetDataProvider _streamDataProvider;
  private readonly SoundPlayer _soundPlayer;
  private readonly Song _song;

  /// <inheritdoc/>
  public event EventHandler? SongEnded;

  /// <inheritdoc/>
  public event EventHandler<TimeSpan>? PositionChanged;

  /// <inheritdoc/>
  public event EventHandler<PlaybackState>? PlaybackStateChanged;

  /// <inheritdoc/>
  public TimeSpan Duration => TimeSpan.FromSeconds(_soundPlayer.Duration);

  /// <inheritdoc/>
  public TimeSpan CurrentTime => TimeSpan.FromSeconds(_soundPlayer.Time);

  /// <inheritdoc/>
  public float Progress => _soundPlayer.Time / _soundPlayer.Duration;

  /// <inheritdoc/>
  public Song Song => _song;

  /// <inheritdoc/>
  public PlaybackState PlaybackState => _soundPlayer.State switch {
    SoundFlowPlaybackState.Playing => PlaybackState.Playing,
    SoundFlowPlaybackState.Paused => PlaybackState.Paused,
    SoundFlowPlaybackState.Stopped => PlaybackState.Stopped,
    _ => throw new UnreachableException("Unknown playback state: " + _soundPlayer.State)
  };

  /// <summary>
  /// Creates a new instance of <see cref="SoundFlowPlaybackService"/>.
  /// </summary>
  /// <param name="audioEngine">The audio engine to use.</param>
  /// <param name="playbackDevice">The playback device to use.</param>
  /// <param name="song">The song to play.</param>
  /// <param name="songStream">The stream of the song to play.</param>
  /// <param name="audioFormat">The format of the audio.</param>
  public SoundFlowPlaybackService(MiniAudioEngine audioEngine, AudioPlaybackDevice playbackDevice, Song song, Stream songStream, AudioFormat audioFormat) {
    _playbackDevice = playbackDevice;
    _song = song;
    _songStream = songStream;
    _audioFormat = audioFormat;

    _streamDataProvider = new AssetDataProvider(audioEngine, audioFormat, songStream);
    _soundPlayer = new SoundPlayer(audioEngine, audioFormat, _streamDataProvider);
    _playbackDevice.MasterMixer.AddComponent(_soundPlayer);
    _streamDataProvider.PositionChanged += (sender, args) => PositionChanged?.Invoke(this, CurrentTime);
    _soundPlayer.PlaybackEnded += (sender, args) => SongEnded?.Invoke(this, EventArgs.Empty);
  }

  /// <inheritdoc/>
  public void Play() {
    if (PlaybackState == PlaybackState.Playing) return;

    Logging.Debug("Playing stream...");
    _soundPlayer.Play();
    PlaybackStateChanged?.Invoke(this, PlaybackState.Playing);
  }

  /// <inheritdoc/>
  public void Pause() {
    if (PlaybackState == PlaybackState.Paused) return;

    Logging.Debug("Pausing stream...");
    _soundPlayer.Pause();
    PlaybackStateChanged?.Invoke(this, PlaybackState.Paused);
  }

  /// <inheritdoc/>
  public void Stop() {
    if (PlaybackState == PlaybackState.Stopped) return;

    Logging.Debug("Stopping stream...");
    _soundPlayer.Stop();
    PlaybackStateChanged?.Invoke(this, PlaybackState.Stopped);
  }

  /// <inheritdoc/>
  public void Seek(TimeSpan position) {
    _soundPlayer.Seek(position);
  }

  /// <inheritdoc/>
  public void Dispose() {
    _playbackDevice.MasterMixer.RemoveComponent(_soundPlayer);
    _soundPlayer.Dispose();
    _streamDataProvider.Dispose();
    _songStream.Dispose();
  }
}