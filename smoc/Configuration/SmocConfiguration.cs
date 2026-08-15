using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public static LogLevel LogLevel { get; set; } = LogLevel.Information;

  /// <summary>
  /// Gets or sets the active streaming service.
  /// </summary>
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public static StreamingService ActiveService { get; set; } = StreamingService.YouTubeMusic;

  /// <summary>
  /// Gets or sets the maximum size of the song cache in bytes.
  /// The default value is 512MB.
  /// A value of 0 means no limit.
  /// </summary>
  public static long SongCacheSizeBytes { get; set; } = 1024 * 1024 * 512; // 512MB

  /// <summary>
  /// Gets or sets the maximum number of songs to cache.
  /// The default value is 0.
  /// A value of 0 means no limit.
  /// </summary>
  public static int SongCacheMaxElements { get; set; } = 0;

  /// <summary>
  /// Gets or sets the maximum size of the album cover cache in bytes.
  /// The default value is 100MB.
  /// A value of 0 means no limit.
  /// </summary>
  public static long AlbumCoverCacheSizeBytes { get; set; } = 1024 * 1024 * 100; // 100MB

  /// <summary>
  /// Gets or sets the maximum number of album covers to cache.
  /// The default value is 0.
  /// A value of 0 means no limit.
  /// </summary>
  public static int AlbumCoverCacheMaxElements { get; set; } = 0;

  /// <summary>
  /// Gets or sets the visualizer refresh rate in frames per second (FPS).
  /// The default value is 24.
  /// </summary>
  public static int VisualizerFps { get; set; } = 24;

  /// <summary>
  /// Gets or sets whether loudness normalization is enabled.
  /// The default value is true.
  /// </summary>
  public static bool EnableLoudnessNormalization { get; set; } = true;

  /// <summary>
  /// Gets or sets the mode for loudness normalization.
  /// The default value is <see cref="LoudnessNormalizationMode.Full"/>.
  /// </summary>
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public static LoudnessNormalizationMode LoudnessNormalizationMode { get; set; } = LoudnessNormalizationMode.Full;

  /// <summary>
  /// Binds configuration settings from the specified <see cref="IConfiguration"/>.
  /// </summary>
  /// <param name="config">The configuration source.</param>
  public static void Bind(IConfiguration config) {
    var section = config.GetSection("SmocConfiguration");
    if (section.Exists()) {
      if (Enum.TryParse<LogLevel>(section["LogLevel"], true, out var logLevel)) LogLevel = logLevel;
      if (Enum.TryParse<StreamingService>(section["ActiveService"], true, out var service)) ActiveService = service;
      if (long.TryParse(section["SongCacheSizeBytes"], out var songCacheSize)) SongCacheSizeBytes = songCacheSize;
      if (int.TryParse(section["SongCacheMaxElements"], out var songCacheMax)) SongCacheMaxElements = songCacheMax;
      if (long.TryParse(section["AlbumCoverCacheSizeBytes"], out var artCacheSize)) AlbumCoverCacheSizeBytes = artCacheSize;
      if (int.TryParse(section["AlbumCoverCacheMaxElements"], out var artCacheMax)) AlbumCoverCacheMaxElements = artCacheMax;
      if (int.TryParse(section["VisualizerFps"], out var fps)) VisualizerFps = fps;
      if (bool.TryParse(section["EnableLoudnessNormalization"], out var norm)) EnableLoudnessNormalization = norm;
      if (Enum.TryParse<LoudnessNormalizationMode>(section["LoudnessNormalizationMode"], true, out var normMode)) LoudnessNormalizationMode = normMode;
    }
    if (Enum.TryParse<LogLevel>(config["SmocConfiguration.LogLevel"], true, out var flatLogLevel)) LogLevel = flatLogLevel;
    if (Enum.TryParse<StreamingService>(config["SmocConfiguration.ActiveService"], true, out var flatService)) ActiveService = flatService;
    if (long.TryParse(config["SmocConfiguration.SongCacheSizeBytes"], out var flatSongCacheSize)) SongCacheSizeBytes = flatSongCacheSize;
    if (int.TryParse(config["SmocConfiguration.SongCacheMaxElements"], out var flatSongCacheMax)) SongCacheMaxElements = flatSongCacheMax;
    if (long.TryParse(config["SmocConfiguration.AlbumCoverCacheSizeBytes"], out var flatArtCacheSize)) AlbumCoverCacheSizeBytes = flatArtCacheSize;
    if (int.TryParse(config["SmocConfiguration.AlbumCoverCacheMaxElements"], out var flatArtCacheMax)) AlbumCoverCacheMaxElements = flatArtCacheMax;
    if (int.TryParse(config["SmocConfiguration.VisualizerFps"], out var flatFps)) VisualizerFps = flatFps;
    if (bool.TryParse(config["SmocConfiguration.EnableLoudnessNormalization"], out var flatNorm)) EnableLoudnessNormalization = flatNorm;
    if (Enum.TryParse<LoudnessNormalizationMode>(config["SmocConfiguration.LoudnessNormalizationMode"], true, out var flatNormMode)) LoudnessNormalizationMode = flatNormMode;
  }
}
