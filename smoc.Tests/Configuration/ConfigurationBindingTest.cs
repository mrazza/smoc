using Microsoft.Extensions.Logging;
using Smoc.Configuration;
using Terminal.Gui.Configuration;
using Xunit;

namespace smoc.Tests.Configuration;

public class ConfigurationBindingTest {
  [Fact]
  public void Bind_SmocConfiguration_FromSection_UsingTuiConfigurationBuilder() {
    var originalConfig = SmocConfiguration.Defaults;
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
          "LoudnessNormalizationMode": "Full"
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
    } finally {
      SmocConfiguration.Defaults = originalConfig;
    }
  }

  [Fact]
  public void Bind_OtherConfigs_UsingTuiConfigurationBuilder() {
    var origListen = ListenHistoryConfig.Defaults;
    var origSoundCloud = SoundCloudConfig.Defaults;
    var origSubsonic = SubsonicConfig.Defaults;
    var origTidal = TidalConfig.Defaults;
    var origYtm = YouTubeMusicConfig.Defaults;

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
        "TidalConfig": {
          "ClientId": "tidal-client-id",
          "ClientSecret": "tidal-client-secret",
          "AccessToken": "tidal-access-token",
          "RefreshToken": "tidal-refresh-token",
          "TokenExpiry": 1700000000,
          "Quality": "LOSSLESS",
          "CountryCode": "GB"
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
        .BindAppSettings<TidalConfig>("TidalConfig", s => TidalConfig.Defaults = s)
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

      Assert.Equal("tidal-client-id", TidalConfig.Defaults.ClientId);
      Assert.Equal("tidal-client-secret", TidalConfig.Defaults.ClientSecret);
      Assert.Equal("tidal-access-token", TidalConfig.Defaults.AccessToken);
      Assert.Equal("tidal-refresh-token", TidalConfig.Defaults.RefreshToken);
      Assert.Equal(1700000000L, TidalConfig.Defaults.TokenExpiry);
      Assert.Equal("LOSSLESS", TidalConfig.Defaults.Quality);
      Assert.Equal("GB", TidalConfig.Defaults.CountryCode);

      Assert.Equal("player123", YouTubeMusicConfig.Defaults.PlayerId);
    } finally {
      ListenHistoryConfig.Defaults = origListen;
      SoundCloudConfig.Defaults = origSoundCloud;
      SubsonicConfig.Defaults = origSubsonic;
      TidalConfig.Defaults = origTidal;
      YouTubeMusicConfig.Defaults = origYtm;
    }
  }
}
