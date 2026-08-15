using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Smoc.Configuration;
using Xunit;

namespace smoc.Tests.Configuration;

public class ConfigurationBindingTest {
  [Fact]
  public void Bind_SmocConfiguration_FromFlatKeys() {
    var json = """
    {
      "SmocConfiguration.LogLevel": "Debug",
      "SmocConfiguration.ActiveService": "Subsonic",
      "SmocConfiguration.SongCacheSizeBytes": 1000000,
      "SmocConfiguration.SongCacheMaxElements": 50,
      "SmocConfiguration.AlbumCoverCacheSizeBytes": 2000000,
      "SmocConfiguration.AlbumCoverCacheMaxElements": 100,
      "SmocConfiguration.VisualizerFps": 60,
      "SmocConfiguration.EnableLoudnessNormalization": false,
      "SmocConfiguration.LoudnessNormalizationMode": "AttenuateOnly"
    }
    """;

    var config = new ConfigurationBuilder()
        .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
        .Build();

    SmocConfiguration.Bind(config);

    Assert.Equal(LogLevel.Debug, SmocConfiguration.LogLevel);
    Assert.Equal(StreamingService.Subsonic, SmocConfiguration.ActiveService);
    Assert.Equal(1000000, SmocConfiguration.SongCacheSizeBytes);
    Assert.Equal(50, SmocConfiguration.SongCacheMaxElements);
    Assert.Equal(2000000, SmocConfiguration.AlbumCoverCacheSizeBytes);
    Assert.Equal(100, SmocConfiguration.AlbumCoverCacheMaxElements);
    Assert.Equal(60, SmocConfiguration.VisualizerFps);
    Assert.False(SmocConfiguration.EnableLoudnessNormalization);
    Assert.Equal(LoudnessNormalizationMode.AttenuateOnly, SmocConfiguration.LoudnessNormalizationMode);
  }

  [Fact]
  public void Bind_SmocConfiguration_FromSection() {
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

    var config = new ConfigurationBuilder()
        .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
        .Build();

    SmocConfiguration.Bind(config);

    Assert.Equal(LogLevel.Trace, SmocConfiguration.LogLevel);
    Assert.Equal(StreamingService.SoundCloud, SmocConfiguration.ActiveService);
    Assert.Equal(5000000, SmocConfiguration.SongCacheSizeBytes);
    Assert.Equal(20, SmocConfiguration.SongCacheMaxElements);
    Assert.Equal(3000000, SmocConfiguration.AlbumCoverCacheSizeBytes);
    Assert.Equal(40, SmocConfiguration.AlbumCoverCacheMaxElements);
    Assert.Equal(30, SmocConfiguration.VisualizerFps);
    Assert.True(SmocConfiguration.EnableLoudnessNormalization);
    Assert.Equal(LoudnessNormalizationMode.Full, SmocConfiguration.LoudnessNormalizationMode);
  }

  [Fact]
  public void Bind_OtherConfigs_FromFlatAndSections() {
    var json = """
    {
      "ListenHistoryConfig.Enabled": false,
      "ListenHistoryConfig.MinimumPositionSeconds": 45,
      "ListenHistoryConfig.MinimumFraction": 0.75,
      "SoundCloudConfig.ClientId": "sc-id",
      "SoundCloudConfig.AuthToken": "sc-token",
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

    var config = new ConfigurationBuilder()
        .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
        .Build();

    ListenHistoryConfig.Bind(config);
    SoundCloudConfig.Bind(config);
    SubsonicConfig.Bind(config);
    YouTubeMusicConfig.Bind(config);

    Assert.False(ListenHistoryConfig.Enabled);
    Assert.Equal(45, ListenHistoryConfig.MinimumPositionSeconds);
    Assert.Equal(0.75, ListenHistoryConfig.MinimumFraction);

    Assert.Equal("sc-id", SoundCloudConfig.ClientId);
    Assert.Equal("sc-token", SoundCloudConfig.AuthToken);

    Assert.Equal("subsonic.local", SubsonicConfig.ServerHost);
    Assert.Equal(4040, SubsonicConfig.ServerPort);
    Assert.Equal("https", SubsonicConfig.ServerScheme);
    Assert.Equal("admin", SubsonicConfig.Username);
    Assert.Equal("secretpassword", SubsonicConfig.Password);
    Assert.False(SubsonicConfig.UseToken);

    Assert.Equal("player123", YouTubeMusicConfig.PlayerId);
  }
}
