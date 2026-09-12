using Terminal.Gui.App;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Smoc.Services.Util;

namespace Smoc.Services.Cast;

/// <summary>
/// Service that proxies media streams over HTTP for Chromecast playback.
/// </summary>
public sealed class StreamingProxyService : IStreamingProxyService {
  private sealed record StreamEntry(Stream Stream, string ContentType, object Lock);

  private readonly ConcurrentDictionary<string, StreamEntry> _streams = new(StringComparer.OrdinalIgnoreCase);
  private readonly object _listenerLock = new();
  private HttpListener? _listener;
  private string? _currentUrl;
  private string? _boundIp;
  private int _boundPort;
  private CancellationTokenSource? _cts;
  private Task? _listenTask;

  /// <inheritdoc/>
  public string? TargetHost { get; set; }

  /// <inheritdoc/>
  public string? CurrentProxyUrl => _currentUrl;

  /// <inheritdoc/>
  public string StartProxy(Stream stream, string contentType) {
    return StartProxy(stream, contentType, TargetHost);
  }

  /// <inheritdoc/>
  public string StartProxy(Stream stream, string contentType, string? targetHost) {
    if (!string.IsNullOrWhiteSpace(targetHost)) {
      TargetHost = targetHost;
    }

    var streamId = Guid.NewGuid().ToString("N");
    _streams[streamId] = new StreamEntry(stream, contentType, new object());

    lock (_listenerLock) {
      EnsureListenerRunning();
      _currentUrl = $"http://{_boundIp}:{_boundPort}/stream/{streamId}";
      Logging.Information($"StreamingProxyService stream registered at {_currentUrl}");
    }

    return _currentUrl;
  }

  /// <inheritdoc/>
  public void StopProxy(string? streamIdOrUrl = null) {
    if (string.IsNullOrWhiteSpace(streamIdOrUrl)) {
      lock (_listenerLock) {
        _cts?.Cancel();
        try {
          _listener?.Stop();
          _listener?.Close();
        } catch {
          // Ignored during cleanup
        }
        _listener = null;
        _streams.Clear();
        _currentUrl = null;
        _cts?.Dispose();
        _cts = null;
        _listenTask = null;
      }
      return;
    }

    var streamId = streamIdOrUrl.TrimEnd("/".ToCharArray()).Split("/".ToCharArray()).Last();
    _streams.TryRemove(streamId, out _);
    _streams.TryRemove(streamIdOrUrl, out _);
  }

  private void EnsureListenerRunning() {
    if (_listener != null && _listener.IsListening) {
      return;
    }

    _boundPort = GetAvailablePort();
    _boundIp = GetLocalIPAddress(TargetHost);

    _listener = new HttpListener();
    _listener.Prefixes.Add($"http://{_boundIp}:{_boundPort}/");
    _listener.Start();

    _cts = new CancellationTokenSource();
    var token = _cts.Token;
    _listenTask = Task.Run(() => ListenLoop(token));
    Logging.Information($"StreamingProxyService listening at http://{_boundIp}:{_boundPort}/");
  }

  private async Task ListenLoop(CancellationToken token) {
    while (!token.IsCancellationRequested && _listener is { IsListening: true }) {
      try {
        var context = await _listener.GetContextAsync();
        _ = Task.Run(() => HandleRequest(context, token), token);
      } catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException) {
        break;
      } catch (Exception ex) {
        Logging.Error($"StreamingProxy listener error: {ex.Message}");
      }
    }
  }

  private async Task HandleRequest(HttpListenerContext context, CancellationToken token) {
    try {
      var request = context.Request;
      var response = context.Response;

      if (!request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
          !request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) {
        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
        response.Close();
        return;
      }

      var path = request.Url?.AbsolutePath.Trim("/".ToCharArray()) ?? string.Empty;
      StreamEntry? entry = null;

      if (path.StartsWith("stream/", StringComparison.OrdinalIgnoreCase)) {
        var streamId = path["stream/".Length..];
        _streams.TryGetValue(streamId, out entry);
      } else if (path.Equals("stream", StringComparison.OrdinalIgnoreCase)) {
        entry = _streams.Values.LastOrDefault();
      }

      if (entry == null) {
        response.StatusCode = (int)HttpStatusCode.NotFound;
        response.Close();
        return;
      }

      var (stream, contentType, streamLock) = entry;
      response.ContentType = contentType;

      if (stream.CanSeek) {
        response.Headers.Set("Accept-Ranges", "bytes");
        long totalLength;
        lock (streamLock) {
          totalLength = stream.Length;
        }

        var rangeHeader = request.Headers["Range"];
        if (!string.IsNullOrWhiteSpace(rangeHeader) &&
            rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) {
          var rangeStr = rangeHeader["bytes=".Length..].Trim();
          long start = 0;
          long end = totalLength - 1;
          bool valid = true;
          var dashIndex = rangeStr.IndexOf("-", StringComparison.Ordinal);

          if (dashIndex >= 0) {
            var firstPart = rangeStr[..dashIndex].Trim();
            var secondPart = rangeStr[(dashIndex + 1)..].Trim();
            if (string.IsNullOrEmpty(firstPart)) {
              if (long.TryParse(secondPart, out var suffix) && suffix > 0) {
                start = Math.Max(0, totalLength - suffix);
                end = totalLength - 1;
              } else {
                valid = false;
              }
            } else if (string.IsNullOrEmpty(secondPart)) {
              if (long.TryParse(firstPart, out var s) && s >= 0 && s < totalLength) {
                start = s;
                end = totalLength - 1;
              } else {
                valid = false;
              }
            } else {
              if (long.TryParse(firstPart, out var s) &&
                  long.TryParse(secondPart, out var e) &&
                  s >= 0 && s <= e && s < totalLength) {
                start = s;
                end = Math.Min(e, totalLength - 1);
              } else {
                valid = false;
              }
            }
          } else {
            valid = false;
          }

          if (!valid) {
            response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
            response.Headers.Set("Content-Range", $"bytes */{totalLength}");
            response.Close();
            return;
          }

          response.StatusCode = (int)HttpStatusCode.PartialContent;
          response.Headers.Set("Content-Range", $"bytes {start}-{end}/{totalLength}");
          long rangeLength = end - start + 1;
          response.ContentLength64 = rangeLength;

          if (request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) {
            response.Close();
            return;
          }

          await StreamRangeAsync(stream, response.OutputStream, start, rangeLength, streamLock, token);
          response.OutputStream.Close();
          return;
        }

        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentLength64 = totalLength;

        if (request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) {
          response.Close();
          return;
        }

        await StreamRangeAsync(stream, response.OutputStream, 0, totalLength, streamLock, token);
        response.OutputStream.Close();
      } else {
        response.StatusCode = (int)HttpStatusCode.OK;
        response.SendChunked = true;

        if (request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) {
          response.Close();
          return;
        }

        byte[] buffer = new byte[65536];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0) {
          await response.OutputStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
        }
        response.OutputStream.Close();
      }
    } catch (Exception ex) when (ex is not OperationCanceledException) {
      if (ex is not (HttpListenerException or IOException or ObjectDisposedException)) {
        Logging.Error($"StreamingProxy error: {ex.Message}");
      }
    } finally {
      try {
        context.Response.Close();
      } catch {
        // Closed
      }
    }
  }

  private static async Task StreamRangeAsync(
    Stream stream,
    Stream output,
    long start,
    long length,
    object streamLock,
    CancellationToken token) {
    long bytesRemaining = length;
    long currentPos = start;
    byte[] buffer = new byte[65536];

    while (bytesRemaining > 0 && !token.IsCancellationRequested) {
      int toRead = (int)Math.Min(buffer.Length, bytesRemaining);
      int bytesRead;
      lock (streamLock) {
        if (stream.Position != currentPos) {
          stream.Seek(currentPos, SeekOrigin.Begin);
        }
        bytesRead = stream.Read(buffer, 0, toRead);
      }

      if (bytesRead == 0) {
        break;
      }

      currentPos += bytesRead;
      bytesRemaining -= bytesRead;
      await output.WriteAsync(buffer.AsMemory(0, bytesRead), token);
    }
  }

  private static int GetAvailablePort() {
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
  }

  private static string GetLocalIPAddress(string? targetHost) {
    if (!string.IsNullOrWhiteSpace(targetHost)) {
      try {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(targetHost, 65530);
        if (socket.LocalEndPoint is IPEndPoint endPoint &&
            !IPAddress.IsLoopback(endPoint.Address) &&
            endPoint.Address.AddressFamily == AddressFamily.InterNetwork) {
          return endPoint.Address.ToString();
        }
      } catch {
        // Fall back to interface enumeration
      }
    }

    var interfaces = NetworkInterface.GetAllNetworkInterfaces();
    foreach (var ni in interfaces) {
      if (ni.OperationalStatus != OperationalStatus.Up ||
          ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) {
        continue;
      }

      var props = ni.GetIPProperties();
      foreach (var ip in props.UnicastAddresses) {
        if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
            !IPAddress.IsLoopback(ip.Address)) {
          return ip.Address.ToString();
        }
      }
    }

    return "127.0.0.1";
  }

  /// <inheritdoc/>
  public void Dispose() {
    StopProxy();
  }
}
