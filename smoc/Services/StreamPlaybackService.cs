using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace Smoc.Services;

internal sealed class StreamPlaybackService : IDisposable {
  private readonly MiniAudioEngine _audioEngine;
  private readonly Stream _songStream;
  private readonly AudioFormat _audioFormat;
  private readonly AudioPlaybackDevice _playbackDevice;
  private readonly AssetDataProvider _streamDataProvider;
  private readonly SoundPlayer _soundPlayer;

  public event EventHandler? StreamEnded;
  public event EventHandler<TimeSpan>? PositionChanged;

  public TimeSpan Duration => TimeSpan.FromSeconds(_soundPlayer.Duration);
  public TimeSpan Time => TimeSpan.FromSeconds(_soundPlayer.Time);
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
    _soundPlayer.Play();
  }

  public void Pause() {
    _soundPlayer.Pause();
  }

  public void Stop() {
    _soundPlayer.Stop();
  }

  public void Dispose() {
    _playbackDevice.MasterMixer.RemoveComponent(_soundPlayer);
    _soundPlayer.Dispose();
    _streamDataProvider.Dispose();
    _songStream.Dispose();
  }
}
