using System.Net;

namespace Smoc.Streaming.YouTubeMusic;

/// <summary>
/// Authenticated session state for YouTube Music.
/// </summary>
/// <param name="Cookies">The parsed session cookies.</param>
/// <param name="Tokens">The generated or stored Proof-of-Origin and Visitor Data tokens.</param>
public record YtmSession(IReadOnlyList<Cookie> Cookies, YtmTokens Tokens);
