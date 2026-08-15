using Microsoft.Extensions.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for Subsonic.
/// </summary>
public static class SubsonicConfig {
  /// <summary>
  /// Gets or sets the Subsonic server host.
  /// </summary>
  public static string? ServerHost { get; set; } = null;

  /// <summary>
  /// Gets or sets the Subsonic server port.
  /// </summary>
  public static int ServerPort { get; set; } = 80;

  /// <summary>
  /// Gets or sets the Subsonic server scheme.
  /// </summary>
  public static string ServerScheme { get; set; } = "http";

  /// <summary>
  /// Gets or sets the Subsonic username.
  /// </summary>
  public static string? Username { get; set; } = null;

  /// <summary>
  /// Gets or sets the Subsonic password.
  /// </summary>
  public static string? Password { get; set; } = null;

  /// <summary>
  /// Gets or sets whether to use a token instead of a plaintext password.
  /// </summary>
  public static bool UseToken { get; set; } = true;

  /// <summary>
  /// Binds configuration settings from the specified <see cref="IConfiguration"/>.
  /// </summary>
  /// <param name="config">The configuration source.</param>
  public static void Bind(IConfiguration config) {
    var section = config.GetSection("SubsonicConfig");
    if (section.Exists()) {
      if (section["ServerHost"] is { } host) ServerHost = host;
      if (int.TryParse(section["ServerPort"], out var port)) ServerPort = port;
      if (section["ServerScheme"] is { } scheme) ServerScheme = scheme;
      if (section["Username"] is { } user) Username = user;
      if (section["Password"] is { } pass) Password = pass;
      if (bool.TryParse(section["UseToken"], out var token)) UseToken = token;
    }
    if (config["SubsonicConfig.ServerHost"] is { } flatHost) ServerHost = flatHost;
    if (int.TryParse(config["SubsonicConfig.ServerPort"], out var flatPort)) ServerPort = flatPort;
    if (config["SubsonicConfig.ServerScheme"] is { } flatScheme) ServerScheme = flatScheme;
    if (config["SubsonicConfig.Username"] is { } flatUser) Username = flatUser;
    if (config["SubsonicConfig.Password"] is { } flatPass) Password = flatPass;
    if (bool.TryParse(config["SubsonicConfig.UseToken"], out var flatToken)) UseToken = flatToken;
  }
}
