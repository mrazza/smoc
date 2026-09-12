using System;
using System.IO;

namespace Smoc.Services.Cast;

/// <summary>
/// Interface for a service that proxies media streams over HTTP.
/// </summary>
public interface IStreamingProxyService : IDisposable {
  /// <summary>
  /// Gets or sets the target host used to discover the best local network interface IP.
  /// </summary>
  string? TargetHost { get; set; }

  /// <summary>
  /// Starts proxying the specified stream and returns the URL to access it.
  /// </summary>
  /// <param name="stream">The stream to proxy.</param>
  /// <param name="contentType">The content type of the stream.</param>
  /// <returns>The URL of the proxied stream.</returns>
  string StartProxy(Stream stream, string contentType);

  /// <summary>
  /// Starts proxying the specified stream towards a target host and returns the URL to access it.
  /// </summary>
  /// <param name="stream">The stream to proxy.</param>
  /// <param name="contentType">The content type of the stream.</param>
  /// <param name="targetHost">The optional target receiver host/IP to probe for the local IP.</param>
  /// <returns>The URL of the proxied stream.</returns>
  string StartProxy(Stream stream, string contentType, string? targetHost);

  /// <summary>
  /// Stops proxying a specific stream or all streams.
  /// </summary>
  /// <param name="streamIdOrUrl">The stream ID or proxy URL to stop; or null to stop all streams.</param>
  void StopProxy(string? streamIdOrUrl = null);

  /// <summary>
  /// Gets the current proxy URL.
  /// </summary>
  string? CurrentProxyUrl { get; }
}
