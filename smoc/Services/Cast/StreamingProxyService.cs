using Terminal.Gui.App;
using System.Net;
using Smoc.Services.Util;

namespace Smoc.Services.Cast;

public sealed class StreamingProxyService : IStreamingProxyService {
    private HttpListener? _listener;
    private Stream? _currentStream;
    private string? _contentType;
    private string? _currentUrl;
    private Task? _listenTask;
    private CancellationTokenSource? _cts;

    public string? CurrentProxyUrl => _currentUrl;

    public string StartProxy(Stream stream, string contentType) {
        StopProxy();

        _currentStream = stream;
        _contentType = contentType;
        
        // Find an available port
        var port = GetAvailablePort();
        var ip = GetLocalIPAddress();
        _currentUrl = $"http://{ip}:{port}/stream";
        Logging.Information($"StreamingProxy starting on {_currentUrl}");
        Logging.Information($"StreamingProxyService started at {_currentUrl}");

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{port}/");
        _listener.Start();

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));

        return _currentUrl;
    }

    public void StopProxy() {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _listener = null;
        _currentStream = null;
        _currentUrl = null;
    }

    private async Task ListenLoop(CancellationToken token) {
        while (!token.IsCancellationRequested && _listener != null) {
            try {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context, token));
            } catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException) {
                break;
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context, CancellationToken token) {
        try {
            var response = context.Response;
            if (_currentStream == null) {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
                return;
            }

            response.ContentType = _contentType;
            response.SendChunked = true;

            // Simple proxying of the stream
            // Note: Chromecast might request ranges, but we'll start with simple streaming
            await _currentStream.CopyToAsync(response.OutputStream, token);
            response.OutputStream.Close();
        } catch (Exception ex) {
            Logging.Error($"StreamingProxy error: {ex.Message}");
        } finally {
            context.Response.Close();
        }
    }

    private int GetAvailablePort() {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private string GetLocalIPAddress() {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList) {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }

    public void Dispose() {
        StopProxy();
    }
}