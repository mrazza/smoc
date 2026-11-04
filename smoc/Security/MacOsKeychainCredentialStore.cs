using System.Runtime.InteropServices;
using System.Text;

namespace Smoc.Security;

/// <summary>
/// Abstraction over macOS Keychain operations for testability.
/// </summary>
public interface IMacOsKeychainAdapter {
  /// <summary>
  /// Gets whether macOS Keychain is supported and available on the current operating system.
  /// </summary>
  bool IsSupportedPlatform { get; }

  /// <summary>
  /// Retrieves a secret stored in macOS Keychain for the given service and account.
  /// </summary>
  Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken);

  /// <summary>
  /// Stores a secret in macOS Keychain for the given service and account.
  /// </summary>
  Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken);

  /// <summary>
  /// Deletes a secret from macOS Keychain for the given service and account.
  /// </summary>
  Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken);
}

/// <summary>
/// P/Invoke implementation of <see cref="IMacOsKeychainAdapter"/> targeting macOS Security.framework.
/// </summary>
internal sealed class NativeMacOsKeychainAdapter : IMacOsKeychainAdapter {
  private const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";
  private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

  public bool IsSupportedPlatform => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

  [DllImport(SecurityFramework)]
  private static extern int SecItemAdd(IntPtr attributes, out IntPtr result);

  [DllImport(SecurityFramework)]
  private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

  [DllImport(SecurityFramework)]
  private static extern int SecItemDelete(IntPtr query);

  [DllImport(SecurityFramework)]
  private static extern int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

  [DllImport(CoreFoundationFramework)]
  private static extern IntPtr CFDictionaryCreate(
      IntPtr allocator,
      IntPtr[] keys,
      IntPtr[] values,
      long numValues,
      IntPtr keyCallBacks,
      IntPtr valueCallBacks);

  [DllImport(CoreFoundationFramework, CharSet = CharSet.Unicode)]
  private static extern IntPtr CFStringCreateWithCharacters(IntPtr alloc, string chars, long numChars);

  [DllImport(CoreFoundationFramework)]
  private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, long length);

  [DllImport(CoreFoundationFramework)]
  private static extern IntPtr CFDataGetBytePtr(IntPtr theData);

  [DllImport(CoreFoundationFramework)]
  private static extern long CFDataGetLength(IntPtr theData);

  [DllImport(CoreFoundationFramework)]
  private static extern void CFRelease(IntPtr cf);

  // Well-known CFString constants exported by Security.framework
  [DllImport(SecurityFramework)]
  private static extern IntPtr SecClassCopyMatching();

  // Common SecItem keys as CFString pointers dynamically resolved or created
  private static IntPtr CreateCFString(string str) => CFStringCreateWithCharacters(IntPtr.Zero, str, str.Length);

  public Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken) {
    if (!IsSupportedPlatform) {
      return Task.FromResult<string?>(null);
    }

    IntPtr kSecClass = CreateCFString("class");
    IntPtr kSecClassGenericPassword = CreateCFString("genp");
    IntPtr kSecAttrService = CreateCFString("svce");
    IntPtr kSecAttrAccount = CreateCFString("acct");
    IntPtr kSecReturnData = CreateCFString("r_Data");
    IntPtr kSecMatchLimit = CreateCFString("m_Limit");
    IntPtr kSecMatchLimitOne = CreateCFString("m_LimitOne");
    IntPtr kCFBooleanTrue = CreateCFString("kCFBooleanTrue");

    IntPtr cfService = CreateCFString(service);
    IntPtr cfAccount = CreateCFString(account);

    IntPtr[] keys = [kSecClass, kSecAttrService, kSecAttrAccount, kSecReturnData, kSecMatchLimit];
    IntPtr[] values = [kSecClassGenericPassword, cfService, cfAccount, kCFBooleanTrue, kSecMatchLimitOne];

    IntPtr query = CFDictionaryCreate(IntPtr.Zero, keys, values, keys.Length, IntPtr.Zero, IntPtr.Zero);
    try {
      int status = SecItemCopyMatching(query, out IntPtr resultPtr);
      if (status == 0 && resultPtr != IntPtr.Zero) {
        try {
          long length = CFDataGetLength(resultPtr);
          IntPtr bytePtr = CFDataGetBytePtr(resultPtr);
          byte[] buffer = new byte[length];
          Marshal.Copy(bytePtr, buffer, 0, (int)length);
          return Task.FromResult<string?>(Encoding.UTF8.GetString(buffer));
        } finally {
          CFRelease(resultPtr);
        }
      }

      return Task.FromResult<string?>(null);
    } finally {
      CFRelease(query);
      CFRelease(cfService);
      CFRelease(cfAccount);
      CFRelease(kSecClass);
      CFRelease(kSecClassGenericPassword);
      CFRelease(kSecAttrService);
      CFRelease(kSecAttrAccount);
      CFRelease(kSecReturnData);
      CFRelease(kSecMatchLimit);
      CFRelease(kSecMatchLimitOne);
      CFRelease(kCFBooleanTrue);
    }
  }

  public Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken) {
    if (!IsSupportedPlatform) {
      throw new PlatformNotSupportedException("macOS Keychain is only supported on macOS.");
    }

    byte[] secretBytes = Encoding.UTF8.GetBytes(secret);

    IntPtr kSecClass = CreateCFString("class");
    IntPtr kSecClassGenericPassword = CreateCFString("genp");
    IntPtr kSecAttrService = CreateCFString("svce");
    IntPtr kSecAttrAccount = CreateCFString("acct");
    IntPtr kSecValueData = CreateCFString("v_Data");

    IntPtr cfService = CreateCFString(service);
    IntPtr cfAccount = CreateCFString(account);
    IntPtr cfSecretData = CFDataCreate(IntPtr.Zero, secretBytes, secretBytes.Length);

    IntPtr[] queryKeys = [kSecClass, kSecAttrService, kSecAttrAccount];
    IntPtr[] queryValues = [kSecClassGenericPassword, cfService, cfAccount];
    IntPtr query = CFDictionaryCreate(IntPtr.Zero, queryKeys, queryValues, queryKeys.Length, IntPtr.Zero, IntPtr.Zero);

    IntPtr[] updateKeys = [kSecValueData];
    IntPtr[] updateValues = [cfSecretData];
    IntPtr updateDict = CFDictionaryCreate(IntPtr.Zero, updateKeys, updateValues, updateKeys.Length, IntPtr.Zero, IntPtr.Zero);

    try {
      int status = SecItemUpdate(query, updateDict);
      if (status != 0) { // Item does not exist, add it
        IntPtr[] addKeys = [kSecClass, kSecAttrService, kSecAttrAccount, kSecValueData];
        IntPtr[] addValues = [kSecClassGenericPassword, cfService, cfAccount, cfSecretData];
        IntPtr addDict = CFDictionaryCreate(IntPtr.Zero, addKeys, addValues, addKeys.Length, IntPtr.Zero, IntPtr.Zero);
        try {
          SecItemAdd(addDict, out _);
        } finally {
          CFRelease(addDict);
        }
      }

      return Task.CompletedTask;
    } finally {
      CFRelease(query);
      CFRelease(updateDict);
      CFRelease(cfSecretData);
      CFRelease(cfService);
      CFRelease(cfAccount);
      CFRelease(kSecClass);
      CFRelease(kSecClassGenericPassword);
      CFRelease(kSecAttrService);
      CFRelease(kSecAttrAccount);
      CFRelease(kSecValueData);
    }
  }

  public Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken) {
    if (!IsSupportedPlatform) {
      return Task.CompletedTask;
    }

    IntPtr kSecClass = CreateCFString("class");
    IntPtr kSecClassGenericPassword = CreateCFString("genp");
    IntPtr kSecAttrService = CreateCFString("svce");
    IntPtr kSecAttrAccount = CreateCFString("acct");

    IntPtr cfService = CreateCFString(service);
    IntPtr cfAccount = CreateCFString(account);

    IntPtr[] queryKeys = [kSecClass, kSecAttrService, kSecAttrAccount];
    IntPtr[] queryValues = [kSecClassGenericPassword, cfService, cfAccount];
    IntPtr query = CFDictionaryCreate(IntPtr.Zero, queryKeys, queryValues, queryKeys.Length, IntPtr.Zero, IntPtr.Zero);

    try {
      SecItemDelete(query);
      return Task.CompletedTask;
    } finally {
      CFRelease(query);
      CFRelease(cfService);
      CFRelease(cfAccount);
      CFRelease(kSecClass);
      CFRelease(kSecClassGenericPassword);
      CFRelease(kSecAttrService);
      CFRelease(kSecAttrAccount);
    }
  }
}

/// <summary>
/// An <see cref="ICredentialStore"/> that uses Apple's native macOS Keychain.
/// </summary>
public sealed class MacOsKeychainCredentialStore : ICredentialStore {
  private readonly IMacOsKeychainAdapter _adapter;

  /// <summary>
  /// Initializes a new instance of <see cref="MacOsKeychainCredentialStore"/> using the native macOS adapter.
  /// </summary>
  public MacOsKeychainCredentialStore()
      : this(new NativeMacOsKeychainAdapter()) {
  }

  /// <summary>
  /// Initializes a new instance of <see cref="MacOsKeychainCredentialStore"/> with a custom adapter for testing.
  /// </summary>
  /// <param name="adapter">The macOS Keychain adapter.</param>
  public MacOsKeychainCredentialStore(IMacOsKeychainAdapter adapter) {
    _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
  }

  /// <inheritdoc />
  public Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    return _adapter.GetSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);

    return _adapter.SetSecretAsync(service, account, secret, cancellationToken);
  }

  /// <inheritdoc />
  public Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    return _adapter.DeleteSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(_adapter.IsSupportedPlatform);
  }
}
