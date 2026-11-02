using System.Text.Json;
using Smoc.Configuration;
using Smoc.Services.Audio;
using Smoc.Streaming;

namespace Smoc.Tests.Services.Audio;

/// <summary>
/// Unit tests for audio device records, configuration, and interface defaults.
/// </summary>
public class AudioDeviceTest {

  /// <summary>
  /// Test implementation of <see cref="IAudioService"/>.
  /// </summary>
  private sealed class TestAudioService : IAudioService {
    public float Volume { get; set; } = 1.0f;
    public int? SelectedDeviceIndex { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<AudioDeviceInfo?>? PlaybackDeviceChanged;

    public void Dispose() {
    }

    public IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, float? loudnessDb, CancellationToken cancellationToken = default) {
      throw new NotImplementedException();
    }

    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices() {
      return [
        new AudioDeviceInfo(0, "Default Speakers", true, SelectedDeviceIndex == 0 || SelectedDeviceIndex == null),
        new AudioDeviceInfo(1, "Headphones", false, SelectedDeviceIndex == 1)
      ];
    }

    public bool SelectPlaybackDevice(int deviceIndex) {
      if (deviceIndex >= 0 && deviceIndex < 2) {
        SelectedDeviceIndex = deviceIndex;
        PlaybackDeviceChanged?.Invoke(this, ActivePlaybackDevice);
        return true;
      }
      return false;
    }

    public AudioDeviceInfo? ActivePlaybackDevice {
      get {
        var devices = GetPlaybackDevices();
        for (int i = 0; i < devices.Count; i++) {
          if (devices[i].IsActive) {
            return devices[i];
          }
        }
        return devices[0];
      }
    }
  }

  /// <summary>
  /// Verifies that <see cref="AudioDeviceInfo"/> properly retains assigned property values.
  /// </summary>
  [Fact]
  public void AudioDeviceInfo_Properties_SetCorrectly() {
    var device = new AudioDeviceInfo(0, "Headphones", false, true);

    Assert.Equal(0, device.Index);
    Assert.Equal("Headphones", device.Name);
    Assert.False(device.IsDefault);
    Assert.True(device.IsActive);
  }

  /// <summary>
  /// Verifies value equality semantics for <see cref="AudioDeviceInfo"/>.
  /// </summary>
  [Fact]
  public void AudioDeviceInfo_Equality_MatchesEqualRecords() {
    var deviceA = new AudioDeviceInfo(1, "Speakers", true, false);
    var deviceB = new AudioDeviceInfo(1, "Speakers", true, false);

    Assert.Equal(deviceA, deviceB);
    Assert.True(deviceA == deviceB);
    Assert.Equal(deviceA.GetHashCode(), deviceB.GetHashCode());
  }

  /// <summary>
  /// Verifies inequality for <see cref="AudioDeviceInfo"/> when fields differ.
  /// </summary>
  [Fact]
  public void AudioDeviceInfo_Inequality_DistinguishesDifferences() {
    var deviceBase = new AudioDeviceInfo(0, "DAC", true, false);
    var diffIndex = new AudioDeviceInfo(1, "DAC", true, false);
    var diffName = new AudioDeviceInfo(0, "USB DAC", true, false);
    var diffDefault = new AudioDeviceInfo(0, "DAC", false, false);
    var diffActive = new AudioDeviceInfo(0, "DAC", true, true);

    Assert.NotEqual(deviceBase, diffIndex);
    Assert.NotEqual(deviceBase, diffName);
    Assert.NotEqual(deviceBase, diffDefault);
    Assert.NotEqual(deviceBase, diffActive);
  }

  /// <summary>
  /// Verifies record non-destructive mutation via with expressions.
  /// </summary>
  [Fact]
  public void AudioDeviceInfo_WithExpression_CopiesWithChanges() {
    var device = new AudioDeviceInfo(2, "Audio Output", false, false);
    var activeDevice = device with { IsActive = true };

    Assert.False(device.IsActive);
    Assert.True(activeDevice.IsActive);
    Assert.Equal(device.Index, activeDevice.Index);
    Assert.Equal(device.Name, activeDevice.Name);
    Assert.Equal(device.IsDefault, activeDevice.IsDefault);
  }

  /// <summary>
  /// Verifies that the default value of <see cref="SmocConfiguration.AudioDevice"/> is null.
  /// </summary>
  [Fact]
  public void SmocConfiguration_AudioDevice_DefaultIsNull() {
    var config = new SmocConfiguration();

    Assert.Null(config.AudioDevice);
  }

  /// <summary>
  /// Verifies that <see cref="SmocConfiguration.AudioDevice"/> can be assigned and retrieved.
  /// </summary>
  [Theory]
  [InlineData("0")]
  [InlineData("1")]
  [InlineData("USB Audio DAC")]
  [InlineData("Built-in Output")]
  public void SmocConfiguration_AudioDevice_CanBeAssignedAndRetrieved(string deviceIdentifier) {
    var config = new SmocConfiguration {
      AudioDevice = deviceIdentifier
    };

    Assert.Equal(deviceIdentifier, config.AudioDevice);
  }

  /// <summary>
  /// Verifies that <see cref="SmocConfiguration.AudioDevice"/> serializes and deserializes accurately with JSON.
  /// </summary>
  [Fact]
  public void SmocConfiguration_AudioDevice_JsonSerializationRoundTrip() {
    var originalConfig = new SmocConfiguration {
      AudioDevice = "External DAC"
    };

    string json = JsonSerializer.Serialize(originalConfig);
    var deserializedConfig = JsonSerializer.Deserialize<SmocConfiguration>(json);

    Assert.NotNull(deserializedConfig);
    Assert.Equal("External DAC", deserializedConfig.AudioDevice);
  }

  /// <summary>
  /// Verifies that <see cref="IAudioService.GetPlaybackDevices"/> returns available devices.
  /// </summary>
  [Fact]
  public void IAudioService_GetPlaybackDevices_ReturnsAvailableDevices() {
    IAudioService service = new TestAudioService();

    var devices = service.GetPlaybackDevices();

    Assert.Equal(2, devices.Count);
    Assert.Equal("Default Speakers", devices[0].Name);
    Assert.Equal("Headphones", devices[1].Name);
  }

  /// <summary>
  /// Verifies that <see cref="IAudioService.SelectPlaybackDevice(int)"/> selects a device by index.
  /// </summary>
  [Fact]
  public void IAudioService_SelectPlaybackDevice_ByIndex_WorksAsExpected() {
    IAudioService service = new TestAudioService();

    bool selected = service.SelectPlaybackDevice(1);
    Assert.True(selected);

    var activeDevice = service.ActivePlaybackDevice;
    Assert.NotNull(activeDevice);
    Assert.Equal("Headphones", activeDevice.Name);
    Assert.True(activeDevice.IsActive);

    bool failedSelect = service.SelectPlaybackDevice(99);
    Assert.False(failedSelect);
  }

  /// <summary>
  /// Verifies that default interface method <see cref="IAudioService.SelectPlaybackDevice(AudioDeviceInfo)"/> delegates to index selection.
  /// </summary>
  [Fact]
  public void IAudioService_SelectPlaybackDevice_ByDeviceInfo_DelegatesToIndex() {
    IAudioService service = new TestAudioService();
    var targetDevice = new AudioDeviceInfo(1, "Headphones", false, false);

    bool selected = service.SelectPlaybackDevice(targetDevice);
    Assert.True(selected);

    var activeDevice = service.ActivePlaybackDevice;
    Assert.NotNull(activeDevice);
    Assert.Equal(1, activeDevice.Index);
    Assert.Equal("Headphones", activeDevice.Name);
    Assert.True(activeDevice.IsActive);

    var invalidDevice = new AudioDeviceInfo(99, "Unknown", false, false);
    bool failedSelect = service.SelectPlaybackDevice(invalidDevice);
    Assert.False(failedSelect);
  }

  /// <summary>
  /// Verifies that <see cref="IAudioService.PlaybackDeviceChanged"/> event can be subscribed to and unsubscribed from.
  /// </summary>
  [Fact]
  public void IAudioService_PlaybackDeviceChanged_AllowsSubscription() {
    IAudioService service = new TestAudioService();
    EventHandler<AudioDeviceInfo?> handler = (_, _) => { };

    service.PlaybackDeviceChanged += handler;
    service.PlaybackDeviceChanged -= handler;
  }

  /// <summary>
  /// Verifies that <see cref="IAudioService.PlaybackDeviceChanged"/> is invoked when a device is selected successfully.
  /// </summary>
  [Fact]
  public void IAudioService_PlaybackDeviceChanged_InvokedWhenSelectPlaybackDeviceSucceeds() {
    IAudioService service = new TestAudioService();
    AudioDeviceInfo? changedDevice = null;
    int eventCount = 0;

    service.PlaybackDeviceChanged += (sender, device) => {
      eventCount++;
      changedDevice = device;
    };

    bool selected = service.SelectPlaybackDevice(1);

    Assert.True(selected);
    Assert.Equal(1, eventCount);
    Assert.NotNull(changedDevice);
    Assert.Equal("Headphones", changedDevice.Name);
    Assert.True(changedDevice.IsActive);
  }

  /// <summary>
  /// Verifies that <see cref="IAudioService.PlaybackDeviceChanged"/> is not invoked when device selection fails.
  /// </summary>
  [Fact]
  public void IAudioService_PlaybackDeviceChanged_NotInvokedWhenSelectPlaybackDeviceFails() {
    IAudioService service = new TestAudioService();
    int eventCount = 0;

    service.PlaybackDeviceChanged += (sender, device) => {
      eventCount++;
    };

    bool selected = service.SelectPlaybackDevice(99);

    Assert.False(selected);
    Assert.Equal(0, eventCount);
  }
}
