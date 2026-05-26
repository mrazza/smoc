using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Terminal.Gui.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Global configuration settings for the SMoC application.
/// </summary>
public static class SmocConfiguration {
  /// <summary>
  /// Gets or sets the minimum log level for the application.
  /// The default value is <see cref="LogLevel.Information"/>.
  /// </summary>
  /// <remarks>
  /// Setting this to <see cref="LogLevel.None"/> will disable all logging.
  /// </remarks>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public static LogLevel LogLevel { get; set; } = LogLevel.Information;

  /// <summary>
  /// Gets or sets the active streaming service.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public static StreamingService ActiveService { get; set; } = StreamingService.YouTubeMusic;

  /// <summary>
  /// Gets or sets the maximum size of the song cache in bytes.
  /// The default value is 512MB.
  /// A value of 0 means no limit.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static long SongCacheSizeBytes { get; set; } = 1024 * 1024 * 512; // 512MB

  /// <summary>
  /// Gets or sets the maximum number of songs to cache.
  /// The default value is 0.
  /// A value of 0 means no limit.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static int SongCacheMaxElements { get; set; } = 0;

  /// <summary>
  /// Gets or sets the maximum size of the album cover cache in bytes.
  /// The default value is 100MB.
  /// A value of 0 means no limit.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static long AlbumCoverCacheSizeBytes { get; set; } = 1024 * 1024 * 100; // 100MB

  /// <summary>
  /// Gets or sets the maximum number of album covers to cache.
  /// The default value is 0.
  /// A value of 0 means no limit.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static int AlbumCoverCacheMaxElements { get; set; } = 0;

  /// <summary>
  /// Gets or sets the visualizer refresh rate in frames per second (FPS).
  /// The default value is 24.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static int VisualizerFps { get; set; } = 24;
}
