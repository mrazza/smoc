using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Terminal.Gui.Configuration;

namespace Smoc.Configuration;

public static class SmocConfiguration {
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public static LogLevel LogLevel { get; set; } = LogLevel.Information;
}
