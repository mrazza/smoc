using System.Diagnostics;
using Smoc.Streaming;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Structs;
using Terminal.Gui.App;
using SoundFlow.Visualization;
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
  private readonly SpectrumAnalyzer? _spectrumAnalyzer;
  private readonly LevelMeterAnalyzer? _levelMeterAnalyzer;
  private float[] _cachedSpectrumData = [];
  private readonly object _spectrumLock = new();

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
  public float[] SpectrumData {
    get {
      if (_spectrumAnalyzer == null) return Array.Empty<float>();
      float[] raw = _spectrumAnalyzer.SpectrumData;
      if (raw == null || raw.Length == 0) return Array.Empty<float>();

      lock (_spectrumLock) {
        if (_cachedSpectrumData.Length != raw.Length) {
          _cachedSpectrumData = new float[raw.Length];
        }
        float peak = _levelMeterAnalyzer?.Peak ?? 1.0f;
        for (int i = 0; i < raw.Length; i++) {
          _cachedSpectrumData[i] = raw[i] * peak;
        }
        return _cachedSpectrumData;
      }
    }
  }

  /// <inheritdoc/>
  public bool IsSpectrumActive {
    get => (_spectrumAnalyzer?.Enabled ?? false) && (_levelMeterAnalyzer?.Enabled ?? false);
    set {
      if (_spectrumAnalyzer != null) {
        _spectrumAnalyzer.Enabled = value;
      }
      if (_levelMeterAnalyzer != null) {
        _levelMeterAnalyzer.Enabled = value;
      }
    }
  }

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

    try {
      _spectrumAnalyzer = new SpectrumAnalyzer(audioFormat, 1024, null);
      _spectrumAnalyzer.Enabled = false;
      _soundPlayer.AddAnalyzer(_spectrumAnalyzer);

      _levelMeterAnalyzer = new LevelMeterAnalyzer(audioFormat, null);
      _levelMeterAnalyzer.Enabled = false;
      _soundPlayer.AddAnalyzer(_levelMeterAnalyzer);
    } catch (Exception ex) {
      Logging.Warning($"Failed to initialize spectrum analyzer: {ex.Message}");
    }

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
    if (_spectrumAnalyzer != null) {
      _soundPlayer.RemoveAnalyzer(_spectrumAnalyzer);
    }
    if (_levelMeterAnalyzer != null) {
      _soundPlayer.RemoveAnalyzer(_levelMeterAnalyzer);
    }
    _playbackDevice.MasterMixer.RemoveComponent(_soundPlayer);
    _soundPlayer.Dispose();
    _streamDataProvider.Dispose();
    _songStream.Dispose();
  }
}