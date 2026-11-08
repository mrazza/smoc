using Microsoft.Extensions.Logging;
using Smoc.Configuration;
using Terminal.Gui.Configuration;
using Xunit;

namespace smoc.Tests.Configuration;

/// <summary>
/// Test collection for tests that mutate shared static configuration defaults.
/// </summary>
[CollectionDefinition("SmocConfigurationTests", DisableParallelization = true)]
public class SmocConfigurationTestCollection {
}

/// <summary>
/// Unit tests for binding configuration sections using <see cref="TuiConfigurationBuilder"/>.
/// </summary>
[Collection("SmocConfigurationTests")]
public class ConfigurationBindingTest {
  /// <summary>
  /// Verifies that <see cref="SmocConfiguration"/> binds correctly from a JSON configuration section.
  /// </summary>
  [Fact]
  public void Bind_SmocConfiguration_FromSection_UsingTuiConfigurationBuilder() {
    var previousDefaults = SmocConfiguration.Defaults;
    try {
      var json = """
      {
        "SmocConfiguration": {
          "LogLevel": "Trace",
          "ActiveService": "SoundCloud",
          "SongCacheSizeBytes": 5000000,
          "SongCacheMaxElements": 20,
          "AlbumCoverCacheSizeBytes": 3000000,
          "AlbumCoverCacheMaxElements": 40,
          "VisualizerFps": 30,
          "EnableLoudnessNormalization": true,
          "LoudnessNormalizationMode": "Full",
          "DeduplicateArtistSingles": false
        }
      }
      """;

      var builder = new TuiConfigurationBuilder("SMoC");
      builder.RuntimeConfig = json;
      builder.BindAppSettings<SmocConfiguration>("SmocConfiguration", s => SmocConfiguration.Defaults = s);

      Assert.Equal(LogLevel.Trace, SmocConfiguration.Defaults.LogLevel);
      Assert.Equal(StreamingService.SoundCloud, SmocConfiguration.Defaults.ActiveService);
      Assert.Equal(5000000, SmocConfiguration.Defaults.SongCacheSizeBytes);
      Assert.Equal(20, SmocConfiguration.Defaults.SongCacheMaxElements);
      Assert.Equal(3000000, SmocConfiguration.Defaults.AlbumCoverCacheSizeBytes);
      Assert.Equal(40, SmocConfiguration.Defaults.AlbumCoverCacheMaxElements);
      Assert.Equal(30, SmocConfiguration.Defaults.VisualizerFps);
      Assert.True(SmocConfiguration.Defaults.EnableLoudnessNormalization);
      Assert.Equal(LoudnessNormalizationMode.Full, SmocConfiguration.Defaults.LoudnessNormalizationMode);
      Assert.False(SmocConfiguration.Defaults.DeduplicateArtistSingles);
    } finally {
      SmocConfiguration.Defaults = previousDefaults;
    }
  }

  /// <summary>
  /// Verifies that <see cref="SmocConfiguration.DeduplicateArtistSingles"/> defaults to true.
  /// </summary>
  [Fact]
  public void Defaults_DeduplicateArtistSingles_IsTrue() {
    var config = new SmocConfiguration();
    Assert.True(config.DeduplicateArtistSingles);
  }

  /// <summary>
  /// Verifies that service and feature configuration classes bind from JSON sections.
  /// </summary>
  [Fact]
  public void Bind_OtherConfigs_UsingTuiConfigurationBuilder() {
    var previousListenHistory = ListenHistoryConfig.Defaults;
    var previousSoundCloud = SoundCloudConfig.Defaults;
    var previousSubsonic = SubsonicConfig.Defaults;
    var previousYouTubeMusic = YouTubeMusicConfig.Defaults;
    try {
      var json = """
      {
        "ListenHistoryConfig": {
          "Enabled": false,
          "MinimumPositionSeconds": 45,
          "MinimumFraction": 0.75
        },
        "SoundCloudConfig": {
          "ClientId": "sc-id",
          "AuthToken": "sc-token"
        },
        "SubsonicConfig": {
          "ServerHost": "subsonic.local",
          "ServerPort": 4040,
          "ServerScheme": "https",
          "Username": "admin",
          "Password": "secretpassword",
          "UseToken": false
        },
        "YouTubeMusicConfig": {
          "PlayerId": "player123"
        }
      }
      """;

      var builder = new TuiConfigurationBuilder("SMoC");
      builder.RuntimeConfig = json;
      builder.BindAppSettings<ListenHistoryConfig>("ListenHistoryConfig", s => ListenHistoryConfig.Defaults = s)
             .BindAppSettings<SoundCloudConfig>("SoundCloudConfig", s => SoundCloudConfig.Defaults = s)
             .BindAppSettings<SubsonicConfig>("SubsonicConfig", s => SubsonicConfig.Defaults = s)
             .BindAppSettings<YouTubeMusicConfig>("YouTubeMusicConfig", s => YouTubeMusicConfig.Defaults = s);

      Assert.False(ListenHistoryConfig.Defaults.Enabled);
      Assert.Equal(45, ListenHistoryConfig.Defaults.MinimumPositionSeconds);
      Assert.Equal(0.75, ListenHistoryConfig.Defaults.MinimumFraction);

      Assert.Equal("sc-id", SoundCloudConfig.Defaults.ClientId);
      Assert.Equal("sc-token", SoundCloudConfig.Defaults.AuthToken);

      Assert.Equal("subsonic.local", SubsonicConfig.Defaults.ServerHost);
      Assert.Equal(4040, SubsonicConfig.Defaults.ServerPort);
      Assert.Equal("https", SubsonicConfig.Defaults.ServerScheme);
      Assert.Equal("admin", SubsonicConfig.Defaults.Username);
      Assert.Equal("secretpassword", SubsonicConfig.Defaults.Password);
      Assert.False(SubsonicConfig.Defaults.UseToken);

      Assert.Equal("player123", YouTubeMusicConfig.Defaults.PlayerId);
    } finally {
      ListenHistoryConfig.Defaults = previousListenHistory;
      SoundCloudConfig.Defaults = previousSoundCloud;
      SubsonicConfig.Defaults = previousSubsonic;
      YouTubeMusicConfig.Defaults = previousYouTubeMusic;
    }
  }
}
