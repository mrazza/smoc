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
  /// composed of the native OS keyring/credential store as primary and environment variables as fallback.
  /// </summary>
  /// <returns>A configured <see cref="ICredentialStore"/> instance.</returns>
  public static ICredentialStore CreateDefault() {
    ICredentialStore nativeStore = CreateNativeStore();
    EnvironmentCredentialStore envStore = new();

    return new CompositeCredentialStore(nativeStore, envStore);
  }

  /// <summary>
  /// Creates the native credential store for the current platform.
  /// </summary>
  /// <returns>An OS-specific <see cref="ICredentialStore"/> implementation.</returns>
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

    return new InMemoryCredentialStore();
  }

  /// <summary>
  /// Creates an in-memory credential store, useful for tests and transient execution.
  /// </summary>
  /// <returns>A new <see cref="InMemoryCredentialStore"/> instance.</returns>
  public static InMemoryCredentialStore CreateInMemory() => new();
}
