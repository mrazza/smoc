using Smoc.Configuration;
using Smoc.Streaming;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Codecs.FFMpeg;
using SoundFlow.Structs;

namespace Smoc.Services.Audio.SoundFlow;

/// <summary>
/// Implementation of <see cref="IAudioService"/> using SoundFlow.
/// </summary>
public sealed class SoundFlowAudioService : IAudioService {
  private readonly MiniAudioEngine _audioEngine;
  private readonly DeviceInfo _playbackDeviceInfo;
  private readonly AudioPlaybackDevice _playbackDevice;
  private readonly SoftClipperModifier _softClipperModifier;
  private float _volume;

  /// <inheritdoc/>
  public float Volume {
    get => _volume;
    set {
      _volume = Math.Clamp(value, 0f, 2.0f);
      _playbackDevice.MasterMixer.Volume = AudioMath.VolumeToGain(_volume);
    }
  }

  /// <summary>
  /// Creates a new instance of <see cref="SoundFlowAudioService"/>.
  /// </summary>
  public SoundFlowAudioService() {
    _audioEngine = new MiniAudioEngine();
    _audioEngine.RegisterCodecFactory(new FFmpegCodecFactory());
    _audioEngine.UpdateAudioDevicesInfo();
    _playbackDeviceInfo = _audioEngine.PlaybackDevices.FirstOrDefault(x => x.IsDefault);
    _playbackDevice = _audioEngine.InitializePlaybackDevice(_playbackDeviceInfo, AudioFormat.DvdHq);
    _softClipperModifier = new SoftClipperModifier();
    _playbackDevice.MasterMixer.AddModifier(_softClipperModifier);
    Volume = AudioMath.DEFAULT_VOLUME;

    _playbackDevice.Start();
  }

  /// <inheritdoc/>
  public IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, CancellationToken cancellationToken = default) {
    return MakePlaybackService(song, stream, codec, null, cancellationToken);
  }

  /// <inheritdoc/>
  public IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, float? loudnessDb, CancellationToken cancellationToken = default) {
    if (_playbackDevice.IsDisposed) throw new InvalidOperationException("Audio service is not initialized.");

    using var decoder = _audioEngine.CreateDecoder(stream, codec, AudioFormat.DvdHq);
    var format = new AudioFormat {
      Format = decoder.SampleFormat,
      Channels = decoder.Channels,
      SampleRate = decoder.SampleRate,
      Layout = AudioFormat.GetLayoutFromChannels(decoder.Channels)
    };
    cancellationToken.ThrowIfCancellationRequested();

    float trackGain = 1.0f;
    if (SmocConfiguration.EnableLoudnessNormalization && loudnessDb.HasValue) {
      bool attenuateOnly = SmocConfiguration.LoudnessNormalizationMode == LoudnessNormalizationMode.AttenuateOnly;
      trackGain = AudioMath.CalculateNormalizationGain(loudnessDb.Value, attenuateOnly);
    }

    return new SoundFlowPlaybackService(_audioEngine, _playbackDevice, song, stream, format, trackGain);
  }

  /// <inheritdoc/>
  public void Dispose() {
    if (!_playbackDevice.IsDisposed) {
      _playbackDevice.MasterMixer.RemoveModifier(_softClipperModifier);
      _playbackDevice.Stop();
    }
    _playbackDevice.Dispose();
    _audioEngine.Dispose();
  }
}
