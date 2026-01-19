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
}
