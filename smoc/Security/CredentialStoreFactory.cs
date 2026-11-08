using Smoc.Security.Linux;
using Smoc.Security.MacOs;
using Smoc.Security.Windows;

namespace Smoc.Security;

/// <summary>
/// Factory for creating platform-appropriate and composite credential store instances.
/// </summary>
public static class CredentialStoreFactory {
  /// <summary>
  /// Creates the default credential store pipeline for the current operating system,
  /// composed of the native OS keyring/credential store as primary (when supported) and environment variables as fallback.
  /// </summary>
  /// <returns>A configured <see cref="ICredentialStore"/> instance.</returns>
  public static ICredentialStore CreateDefault() {
    EnvironmentCredentialStore envStore = new();
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()) {
      return new CompositeCredentialStore(CreateNativeStore(), envStore);
    }

    return new CompositeCredentialStore(envStore);
  }

  /// <summary>
  /// Creates the native credential store for the current platform.
  /// </summary>
  /// <returns>An OS-specific <see cref="ICredentialStore"/> implementation.</returns>
  /// <exception cref="PlatformNotSupportedException">Thrown when no native store is supported on the current operating system.</exception>
  public static ICredentialStore CreateNativeStore() {
    if (OperatingSystem.IsLinux()) {
      return new LinuxSecretServiceCredentialStore();
    }

    if (OperatingSystem.IsMacOS()) {
      return new MacOsKeychainCredentialStore();
    }

    if (OperatingSystem.IsWindows()) {
      return new WindowsCredentialManagerCredentialStore();
    }

    throw new PlatformNotSupportedException("No native credential store supported on this operating system.");
  }
}
