namespace Smoc.Configuration;

/// <summary>
/// Provides default filesystem paths for application configuration and credential storage.
/// </summary>
public static class SmocPaths {
  /// <summary>
  /// Gets the default configuration directory (~/.config/smoc).
  /// </summary>
  public static readonly string DefaultConfigDir = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "smoc");

  /// <summary>
  /// Gets the default path to config.json.
  /// </summary>
  public static readonly string DefaultConfigPath = Path.Combine(DefaultConfigDir, "config.json");

  /// <summary>
  /// Gets the default path to cookies.txt.
  /// </summary>
  public static readonly string DefaultCookiesPath = Path.Combine(DefaultConfigDir, "cookies.txt");

  /// <summary>
  /// Gets the default path to tokens.json.
  /// </summary>
  public static readonly string DefaultTokensPath = Path.Combine(DefaultConfigDir, "tokens.json");

  /// <summary>
  /// Gets the default log file path pattern.
  /// </summary>
  public static readonly string DefaultLogPath = Path.Combine(DefaultConfigDir, "log_{Date}.txt");
}
