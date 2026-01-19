namespace Smoc.Streaming.YouTubeMusic;

/// <summary>
/// Tokens required for authenticated YouTube Music requests.
/// </summary>
/// <param name="PoToken">Proof-of-Origin token.</param>
/// <param name="RolloutToken">Rollout token.</param>
/// <param name="VisitorData">Visitor token.</param>
public record YtmTokens(string PoToken, string RolloutToken, string VisitorData);
