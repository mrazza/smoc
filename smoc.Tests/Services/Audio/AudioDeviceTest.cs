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
  /// Minimal implementation of <see cref="IAudioService"/> using default interface methods.
  /// </summary>
  private sealed class DefaultAudioService : IAudioService {
    public float Volume { get; set; } = 1.0f;

    public void Dispose() {
    }

    public IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, float? loudnessDb, CancellationToken cancellationToken = default) {
      throw new NotImplementedException();
    }
  }

  /// <summary>
  /// Custom implementation of <see cref="IAudioService"/> overriding default interface methods.
  /// </summary>
  private sealed class OverridingAudioService : IAudioService {
    public float Volume { get; set; } = 1.0f;
    public string? SelectedDevice { get; private set; }

    public void Dispose() {
    }

    public IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, float? loudnessDb, CancellationToken cancellationToken = default) {
      throw new NotImplementedException();
    }

    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices() {
      return [
        new AudioDeviceInfo(0, "Default Speakers", true, SelectedDevice == "0" || SelectedDevice == "Default Speakers"),
        new AudioDeviceInfo(1, "Headphones", false, SelectedDevice == "1" || SelectedDevice == "Headphones")
      ];
    }

    public bool SelectPlaybackDevice(string deviceIndexOrName) {
      if (deviceIndexOrName == "0" || deviceIndexOrName == "1" ||
          deviceIndexOrName == "Default Speakers" || deviceIndexOrName == "Headphones") {
        SelectedDevice = deviceIndexOrName;
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
  /// Verifies that default <see cref="IAudioService.GetPlaybackDevices"/> returns an empty list.
  /// </summary>
  [Fact]
  public void IAudioService_GetPlaybackDevices_DefaultReturnsEmpty() {
    IAudioService service = new DefaultAudioService();

    var devices = service.GetPlaybackDevices();

    Assert.NotNull(devices);
    Assert.Empty(devices);
  }

  /// <summary>
  /// Verifies that default <see cref="IAudioService.SelectPlaybackDevice"/> returns false.
  /// </summary>
  [Fact]
  public void IAudioService_SelectPlaybackDevice_DefaultReturnsFalse() {
    IAudioService service = new DefaultAudioService();

    bool resultIndex = service.SelectPlaybackDevice("0");
    bool resultName = service.SelectPlaybackDevice("Headphones");

    Assert.False(resultIndex);
    Assert.False(resultName);
  }

  /// <summary>
  /// Verifies that default <see cref="IAudioService.ActivePlaybackDevice"/> returns null.
  /// </summary>
  [Fact]
  public void IAudioService_ActivePlaybackDevice_DefaultReturnsNull() {
    IAudioService service = new DefaultAudioService();

    Assert.Null(service.ActivePlaybackDevice);
  }

  /// <summary>
  /// Verifies that implementations can override the default interface methods.
  /// </summary>
  [Fact]
  public void IAudioService_OverridingImplementation_WorksAsExpected() {
    IAudioService service = new OverridingAudioService();

    var initialDevices = service.GetPlaybackDevices();
    Assert.Equal(2, initialDevices.Count);
    Assert.Equal("Default Speakers", initialDevices[0].Name);
    Assert.Equal("Headphones", initialDevices[1].Name);

    bool selected = service.SelectPlaybackDevice("Headphones");
    Assert.True(selected);

    var activeDevice = service.ActivePlaybackDevice;
    Assert.NotNull(activeDevice);
    Assert.Equal("Headphones", activeDevice.Name);
    Assert.True(activeDevice.IsActive);

    bool failedSelect = service.SelectPlaybackDevice("NonExistentDevice");
    Assert.False(failedSelect);
  }
}
