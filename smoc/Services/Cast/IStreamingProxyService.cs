using System;
using System.IO;

namespace Smoc.Services.Cast;

/// <summary>
/// Interface for a service that proxies media streams over HTTP.
/// </summary>
public interface IStreamingProxyService : IDisposable {
    /// <summary>
    /// Starts the proxy for the specified stream.
    /// </summary>
    /// <param name="stream">The stream to proxy.</param>
    /// <param name="contentType">The content type of the stream.</param>
    /// <returns>The URL of the proxied stream.</returns>
    string StartProxy(Stream stream, string contentType);

    /// <summary>
    /// Stops the proxy.
    /// </summary>
    void StopProxy();

    /// <summary>
    /// Gets the current proxy URL.
    /// </summary>
    string? CurrentProxyUrl { get; }
}