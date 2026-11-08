namespace Smoc.Cli.Auth;

/// <summary>
/// Parses service target names and aliases.
/// </summary>
public static class AuthServiceTargetParser {
  /// <summary>
  /// Attempts to parse a service name or alias into an <see cref="AuthServiceTarget"/>.
  /// </summary>
  /// <param name="serviceName">The raw service name input.</param>
  /// <param name="target">When this method returns, contains the parsed target if successful.</param>
  /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
  public static bool TryParse(string? serviceName, out AuthServiceTarget target) {
    if (!string.IsNullOrWhiteSpace(serviceName)) {
      switch (serviceName.Trim().ToLowerInvariant()) {
        case "subsonic":
          target = AuthServiceTarget.Subsonic;
          return true;
        case "soundcloud":
          target = AuthServiceTarget.SoundCloud;
          return true;
        case "youtube":
        case "youtubemusic":
        case "ytm":
          target = AuthServiceTarget.YouTubeMusic;
          return true;
        case "all":
          target = AuthServiceTarget.All;
          return true;
      }
    }

    target = default;
    return false;
  }
}

