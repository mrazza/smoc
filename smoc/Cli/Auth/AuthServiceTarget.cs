namespace Smoc.Cli.Auth;

/// <summary>
/// Identifies target services for authentication CLI operations.
/// </summary>
public enum AuthServiceTarget {
  /// <summary>
  /// Subsonic-compatible servers.
  /// </summary>
  Subsonic,

  /// <summary>
  /// SoundCloud.
  /// </summary>
  SoundCloud,

  /// <summary>
  /// YouTube Music.
  /// </summary>
  YouTubeMusic,

  /// <summary>
  /// All supported streaming services.
  /// </summary>
  All
}

