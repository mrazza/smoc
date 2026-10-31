namespace Smoc.Services.Audio;

/// <summary>
/// Represents an available audio playback device.
/// </summary>
/// <param name="Index">The 0-based index of the playback device.</param>
/// <param name="Name">The human-readable name of the playback device.</param>
/// <param name="IsDefault">Whether this device is the system default output device.</param>
/// <param name="IsActive">Whether this device is currently selected for playback in SMoC.</param>
public sealed record AudioDeviceInfo(int Index, string Name, bool IsDefault, bool IsActive);
