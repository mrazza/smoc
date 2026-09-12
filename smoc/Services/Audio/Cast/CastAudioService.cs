using Sharpcaster.Models;
using Smoc.Services.Cast;
using Smoc.Streaming;
using Smoc.Services.Audio;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Smoc.Services.Audio.Cast;

/// <summary>
/// Audio service that plays audio on a Google Cast device.
/// </summary>
public sealed class CastAudioService : IAudioService {
  /// <summary>
  /// The default media receiver application ID.
  /// </summary>
  public const string DefaultMediaReceiverAppId = "CC1AD845";

  private readonly ChromecastReceiver _device;
  private readonly IStreamingProxyService _proxyService;
  private readonly IChromecastClient _client;
  private readonly SemaphoreSlim _connectionLock = new(1, 1);
  private readonly CancellationTokenSource _disposeCts = new();
  private float _volume = 1.0f;

  /// <summary>
  /// Initializes a new instance of the <see cref="CastAudioService"/> class.
  /// </summary>
  /// <param name="device">The target Chromecast receiver.</param>
  /// <param name="proxyService">The streaming proxy service.</param>
  /// <param name="client">Optional Chromecast client wrapper.</param>
  public CastAudioService(
    ChromecastReceiver device,
    IStreamingProxyService proxyService,
    IChromecastClient? client = null) {
    _device = device;
    _proxyService = proxyService;
    _client = client ?? new ChromecastClientWrapper();
    _proxyService.TargetHost = _device.DeviceUri?.Host;
  }

  /// <inheritdoc/>
  public float Volume {
    get => _volume;
    set {
      _volume = Math.Clamp(value, 0.0f, 1.0f);
      _ = _client.SetVolumeAsync(_volume, _disposeCts.Token).ContinueWith(t => {
        if (t.IsFaulted && t.Exception != null) {
          Terminal.Gui.App.Logging.Error($"Failed to set Cast volume: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
        }
      }, TaskScheduler.Default);
    }
  }

  /// <summary>
  /// Ensures that the client is connected to the receiver and the default media receiver app is running.
  /// </summary>
  /// <param name="cancellationToken">A token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default) {
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
    await _connectionLock.WaitAsync(linkedCts.Token);
    try {
      await _client.EnsureConnectedAndLaunchedAsync(_device, DefaultMediaReceiverAppId, linkedCts.Token);
    } finally {
      try {
        _connectionLock.Release();
      } catch (ObjectDisposedException) {
        // Ignored if service disposed during connection attempt
      }
    }
  }

  /// <summary>
  /// Connects to the Chromecast device and launches the default media receiver.
  /// </summary>
  /// <param name="cancellationToken">A token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public async Task ConnectAsync(CancellationToken cancellationToken = default) {
    await EnsureConnectedAsync(cancellationToken);
  }

  /// <summary>
  /// Creates a new playback service for the given stream and codec.
  /// </summary>
  public IPlaybackService MakePlaybackService(
    Song song,
    Stream stream,
    string codec,
    CancellationToken cancellationToken = default) =>
    MakePlaybackService(song, stream, codec, null, cancellationToken);

  /// <inheritdoc/>
  public IPlaybackService MakePlaybackService(
    Song song,
    Stream stream,
    string codec,
    float? loudnessDb,
    CancellationToken cancellationToken = default) {
    var mimeType = MapContentTypeToMimeType(codec);
    var url = _proxyService.StartProxy(stream, mimeType);
    return new CastPlaybackService(_client, song, stream, url, _proxyService, mimeType, EnsureConnectedAsync);
  }

  internal static string MapContentTypeToMimeType(string contentType) {
    var lower = contentType.ToLowerInvariant();
    if (lower.Contains("flac")) return "audio/flac";
    if (lower.Contains("m4a") || lower.Contains("mp4")) return "audio/mp4";
    if (lower.Contains("aac")) return "audio/aac";
    if (lower.Contains("ogg")) return "audio/ogg";
    if (lower.Contains("wav")) return "audio/wav";
    return "audio/mpeg";
  }

  /// <inheritdoc/>
  public void Dispose() {
    _disposeCts.Cancel();
    try {
      _client.DisconnectAsync().Wait(TimeSpan.FromSeconds(1));
    } catch {
      // Suppress exceptions on dispose
    }
    _client.Dispose();
    _connectionLock.Dispose();
    _disposeCts.Dispose();
  }
}
