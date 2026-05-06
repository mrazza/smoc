using System.Text.RegularExpressions;

namespace Smoc.Streaming.SoundCloud.Util;

public static class SoundCloudDiscovery {
  private static readonly Regex ScriptRegex = new Regex("<script[^>]+src=\"([^\"]+)\"", RegexOptions.IgnoreCase);
  private static readonly Regex ClientIdRegex = new Regex("client_id:\"([a-zA-Z0-9]{32})\"");

  public static IEnumerable<string> ExtractScriptUrls(string html) {
    return ScriptRegex.Matches(html).Select(m => m.Groups[1].Value);
  }

  public static string? ExtractClientId(string scriptContent) {
    var match = ClientIdRegex.Match(scriptContent);
    return match.Success ? match.Groups[1].Value : null;
  }
}