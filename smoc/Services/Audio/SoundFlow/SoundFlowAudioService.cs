using Smoc.Configuration;
using Smoc.Streaming;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Codecs.FFMpeg;
using SoundFlow.Structs;
using Terminal.Gui.App;

namespace Smoc.Services.Audio.SoundFlow;

/// <summary>
/// Implementation of <see cref="IAudioService"/> using SoundFlow.
/// </summary>
public sealed class SoundFlowAudioService : IAudioService {
  private readonly MiniAudioEngine _audioEngine;
  private DeviceInfo _playbackDeviceInfo;
  private AudioPlaybackDevice _playbackDevice;
  private readonly SoftClipperModifier _softClipperModifier;
  private readonly object _deviceLock = new();
  private readonly List<WeakReference<SoundFlowPlaybackService>> _activeServices = [];
  private float _volume;

  /// <inheritdoc/>
  public float Volume {
    get => _volume;
    set {
      _volume = Math.Clamp(value, 0f, 2.0f);
      lock (_deviceLock) {
        if (!_playbackDevice.IsDisposed) {
          _playbackDevice.MasterMixer.Volume = AudioMath.VolumeToGain(_volume);
        }
      }
    }
  }

  /// <inheritdoc/>
  public AudioDeviceInfo? ActivePlaybackDevice {
    get {
      lock (_deviceLock) {
        if (_playbackDevice.IsDisposed) {
          return null;
        }

        var devices = _audioEngine.PlaybackDevices;
        int activeIndex = -1;
        if (devices != null) {
          for (int i = 0; i < devices.Length; i++) {
            if (devices[i].Equals(_playbackDeviceInfo) ||
                (devices[i].Name != null && devices[i].Name == _playbackDeviceInfo.Name)) {
              activeIndex = i;
              break;
            }
          }
        }

        return new AudioDeviceInfo(
          activeIndex,
          _playbackDeviceInfo.Name ?? string.Empty,
          _playbackDeviceInfo.IsDefault,
          IsActive: true);
      }
    }
  }

  /// <summary>
  /// Creates a new instance of <see cref="SoundFlowAudioService"/>.
  /// </summary>
  public SoundFlowAudioService() {
    _audioEngine = new MiniAudioEngine();
    _audioEngine.RegisterCodecFactory(new FFmpegCodecFactory());
    _audioEngine.UpdateAudioDevicesInfo();

    DeviceInfo targetDevice = default;
    bool foundConfigured = false;
    var devices = _audioEngine.PlaybackDevices;

    if (!string.IsNullOrWhiteSpace(SmocConfiguration.Defaults.AudioDevice) && devices != null && devices.Length > 0) {
      foundConfigured = TryFindDevice(SmocConfiguration.Defaults.AudioDevice, devices, out targetDevice);
    }

    if (!foundConfigured && devices != null && devices.Length > 0) {
      targetDevice = devices.FirstOrDefault(x => x.IsDefault);
      if (string.IsNullOrEmpty(targetDevice.Name) && devices.Length > 0) {
        targetDevice = devices[0];
      }
    }

    _playbackDeviceInfo = targetDevice;
    _playbackDevice = _audioEngine.InitializePlaybackDevice(_playbackDeviceInfo, AudioFormat.DvdHq);
    _softClipperModifier = new SoftClipperModifier();
    _playbackDevice.MasterMixer.AddModifier(_softClipperModifier);
    Volume = AudioMath.DEFAULT_VOLUME;

    _playbackDevice.Start();
  }

  /// <inheritdoc/>
  public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices() {
    lock (_deviceLock) {
      _audioEngine.UpdateAudioDevicesInfo();
      var devices = _audioEngine.PlaybackDevices;
      if (devices == null || devices.Length == 0) {
        return [];
      }

      var result = new List<AudioDeviceInfo>(devices.Length);
      for (int i = 0; i < devices.Length; i++) {
        var device = devices[i];
        bool isActive = !_playbackDevice.IsDisposed &&
                        (device.Equals(_playbackDeviceInfo) ||
                         (device.Name != null && device.Name == _playbackDeviceInfo.Name));
        result.Add(new AudioDeviceInfo(i, device.Name ?? string.Empty, device.IsDefault, isActive));
      }

      return result;
    }
  }

  /// <inheritdoc/>
  public bool SelectPlaybackDevice(string deviceIndexOrName) {
    if (string.IsNullOrWhiteSpace(deviceIndexOrName)) {
      return false;
    }

    lock (_deviceLock) {
      _audioEngine.UpdateAudioDevicesInfo();
      var devices = _audioEngine.PlaybackDevices;
      if (devices == null || devices.Length == 0) {
        return false;
      }

      if (!TryFindDevice(deviceIndexOrName, devices, out var targetDevice)) {
        return false;
      }

      AudioPlaybackDevice newPlaybackDevice;
      try {
        newPlaybackDevice = _audioEngine.InitializePlaybackDevice(targetDevice, AudioFormat.DvdHq);
      } catch (Exception ex) {
        Logging.Error($"Failed to initialize audio playback device '{targetDevice.Name}': {ex.Message}");
        return false;
      }

      newPlaybackDevice.MasterMixer.AddModifier(_softClipperModifier);
      newPlaybackDevice.MasterMixer.Volume = AudioMath.VolumeToGain(_volume);

      CleanDeadServices();
      for (int i = 0; i < _activeServices.Count; i++) {
        if (_activeServices[i].TryGetTarget(out var service)) {
          service.SwitchPlaybackDevice(newPlaybackDevice);
        }
      }

      newPlaybackDevice.Start();

      var oldDevice = _playbackDevice;
      if (!oldDevice.IsDisposed) {
        oldDevice.MasterMixer.RemoveModifier(_softClipperModifier);
        oldDevice.Stop();
        oldDevice.Dispose();
      }

      _playbackDeviceInfo = targetDevice;
      _playbackDevice = newPlaybackDevice;
      return true;
    }
  }

  /// <inheritdoc/>
  public IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, CancellationToken cancellationToken = default) {
    return MakePlaybackService(song, stream, codec, null, cancellationToken);
  }

  /// <inheritdoc/>
  public IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, float? loudnessDb, CancellationToken cancellationToken = default) {
    lock (_deviceLock) {
      if (_playbackDevice.IsDisposed) {
        throw new InvalidOperationException("Audio service is not initialized.");
      }

      AudioFormat format;
      using (var decoder = _audioEngine.CreateDecoder(stream, codec, AudioFormat.DvdHq)) {
        format = new AudioFormat {
          Format = decoder.SampleFormat,
          Channels = decoder.Channels,
          SampleRate = decoder.SampleRate,
          Layout = AudioFormat.GetLayoutFromChannels(decoder.Channels)
        };
      }
      if (stream.CanSeek) {
        stream.Seek(0, SeekOrigin.Begin);
      }
      cancellationToken.ThrowIfCancellationRequested();

      float trackGain = 1.0f;
      if (SmocConfiguration.Defaults.EnableLoudnessNormalization && loudnessDb.HasValue) {
        bool attenuateOnly = SmocConfiguration.Defaults.LoudnessNormalizationMode == LoudnessNormalizationMode.AttenuateOnly;
        trackGain = AudioMath.CalculateNormalizationGain(loudnessDb.Value, attenuateOnly);
      }

      var service = new SoundFlowPlaybackService(_audioEngine, _playbackDevice, song, stream, format, trackGain);
      CleanDeadServices();
      _activeServices.Add(new WeakReference<SoundFlowPlaybackService>(service));
      return service;
    }
  }

  /// <inheritdoc/>
  public void Dispose() {
    lock (_deviceLock) {
      if (!_playbackDevice.IsDisposed) {
        _playbackDevice.MasterMixer.RemoveModifier(_softClipperModifier);
        _playbackDevice.Stop();
      }
      _playbackDevice.Dispose();
      _audioEngine.Dispose();
    }
  }

  private void CleanDeadServices() {
    _activeServices.RemoveAll(x => !x.TryGetTarget(out _));
  }

  private static bool TryFindDevice(string deviceIndexOrName, DeviceInfo[] devices, out DeviceInfo targetDevice) {
    targetDevice = default;
    if (string.IsNullOrWhiteSpace(deviceIndexOrName) || devices.Length == 0) {
      return false;
    }

    string query = deviceIndexOrName.Trim();
    if (int.TryParse(query, out int index)) {
      if (index >= 0 && index < devices.Length) {
        targetDevice = devices[index];
        return true;
      }
      return false;
    }

    for (int i = 0; i < devices.Length; i++) {
      var d = devices[i];
      if (d.Name != null && d.Name.Equals(query, StringComparison.OrdinalIgnoreCase)) {
        targetDevice = d;
        return true;
      }
    }

    for (int i = 0; i < devices.Length; i++) {
      var d = devices[i];
      if (d.Name != null && d.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) {
        targetDevice = d;
        return true;
      }
    }

    return false;
  }
}
