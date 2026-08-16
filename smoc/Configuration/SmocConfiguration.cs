using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Smoc.Configuration;

/// <summary>
/// Global configuration settings for the SMoC application.
/// </summary>
public class SmocConfiguration {
  /// <summary>
  /// Gets or sets the minimum log level for the application.
  /// The default value is <see cref="LogLevel.Information"/>.
  /// </summary>
  /// <remarks>
  /// Setting this to <see cref="LogLevel.None"/> will disable all logging.
  /// </remarks>
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public LogLevel LogLevel { get; set; } = LogLevel.Information;

  /// <summary>
  /// Gets or sets the active streaming service.
  /// </summary>
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public StreamingService ActiveService { get; set; } = StreamingService.YouTubeMusic;

  /// <summary>
  /// Gets or sets the maximum size of the song cache in bytes.
  /// The default value is 512MB.
  /// A value of 0 means no limit.
  /// </summary>
  public long SongCacheSizeBytes { get; set; } = 1024 * 1024 * 512; // 512MB

  /// <summary>
  /// Gets or sets the maximum number of songs to cache.
  /// The default value is 0.
  /// A value of 0 means no limit.
  /// </summary>
  public int SongCacheMaxElements { get; set; } = 0;

  /// <summary>
  /// Gets or sets the maximum size of the album cover cache in bytes.
  /// The default value is 100MB.
  /// A value of 0 means no limit.
  /// </summary>
  public long AlbumCoverCacheSizeBytes { get; set; } = 1024 * 1024 * 100; // 100MB

  /// <summary>
  /// Gets or sets the maximum number of album covers to cache.
  /// The default value is 0.
  /// A value of 0 means no limit.
  /// </summary>
  public int AlbumCoverCacheMaxElements { get; set; } = 0;

  /// <summary>
  /// Gets or sets the visualizer refresh rate in frames per second (FPS).
  /// The default value is 24.
  /// </summary>
  public int VisualizerFps { get; set; } = 24;

  /// <summary>
  /// Gets or sets whether loudness normalization is enabled.
  /// The default value is true.
  /// </summary>
  public bool EnableLoudnessNormalization { get; set; } = true;

  /// <summary>
  /// Gets or sets the mode for loudness normalization.
  /// The default value is <see cref="LoudnessNormalizationMode.Full"/>.
  /// </summary>
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public LoudnessNormalizationMode LoudnessNormalizationMode { get; set; } = LoudnessNormalizationMode.Full;

  /// <summary>
  /// The static facade instance containing the current effective values.
  /// </summary>
  public static SmocConfiguration Defaults { get; set; } = new();
}
