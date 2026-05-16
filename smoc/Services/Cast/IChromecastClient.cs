using Sharpcaster.Models;
using Sharpcaster.Models.Media;
using System;
using System.Threading.Tasks;

namespace Smoc.Services.Cast;

/// <summary>
/// Interface for a Google Cast client.
/// </summary>
public interface IChromecastClient : IDisposable {
    /// <summary>
    /// Occurs when the media status of the connected device changes.
    /// </summary>
    event EventHandler<MediaStatus>? MediaStatusChanged;

    /// <summary>
    /// Connects to a Chromecast receiver.
    /// </summary>
    /// <param name="receiver">The receiver to connect to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConnectChromecast(ChromecastReceiver receiver);

    /// <summary>
    /// Disconnects from the currently connected device.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisconnectAsync();

    /// <summary>
    /// Launches an application on the connected device.
    /// </summary>
    /// <param name="applicationId">The ID of the application to launch.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LaunchApplicationAsync(string applicationId);

    /// <summary>
    /// Sets the volume level of the connected device.
    /// </summary>
    /// <param name="level">The volume level (0.0 to 1.0).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetVolumeAsync(float level);

    /// <summary>
    /// Loads media on the connected device.
    /// </summary>
    /// <param name="media">The media to load.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LoadAsync(Media media);

    /// <summary>
    /// Starts playback on the connected device.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PlayAsync();

    /// <summary>
    /// Pauses playback on the connected device.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PauseAsync();

    /// <summary>
    /// Stops playback on the connected device.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Seeks to a specific position in the media.
    /// </summary>
    /// <param name="seconds">The position to seek to, in seconds.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeekAsync(double seconds);

    /// <summary>
    /// Gets or sets the volume level of the connected device.
    /// </summary>
    float Volume { get; set; }
}