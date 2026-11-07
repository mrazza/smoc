using System.Runtime.InteropServices;
using System.Text;

namespace Smoc.Security.MacOs;

/// <summary>
/// P/Invoke implementation of <see cref="IMacOsKeychainAdapter"/> targeting macOS Security.framework.
/// </summary>
internal sealed class NativeMacOsKeychainAdapter : IMacOsKeychainAdapter {
  private const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";
  private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

  private const int ErrSecSuccess = 0;
  private const int ErrSecItemNotFound = -25300;

  private static readonly Lazy<KeychainSymbols?> _symbols = new(() => {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      return null;
    }

    return KeychainSymbols.Load();
  });

  /// <inheritdoc />
  public bool IsSupportedPlatform => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

  /// <inheritdoc />
  public Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken) {
    if (!IsSupportedPlatform || _symbols.Value is not { } sym) {
      return Task.FromResult<string?>(null);
    }

    cancellationToken.ThrowIfCancellationRequested();

    IntPtr cfService = CreateCFString(service);
    IntPtr cfAccount = CreateCFString(account);
    try {
      IntPtr[] keys = [sym.SecClass, sym.SecAttrService, sym.SecAttrAccount, sym.SecReturnData, sym.SecMatchLimit];
      IntPtr[] values = [sym.SecClassGenericPassword, cfService, cfAccount, sym.CFBooleanTrue, sym.SecMatchLimitOne];

      IntPtr query = CFDictionaryCreate(IntPtr.Zero, keys, values, keys.Length, sym.KeyCallbacks, sym.ValueCallbacks);
      try {
        int status = SecItemCopyMatching(query, out IntPtr resultPtr);
        if (status == ErrSecSuccess && resultPtr != IntPtr.Zero) {
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

        if (status == ErrSecItemNotFound) {
          return Task.FromResult<string?>(null);
        }

        throw new InvalidOperationException($"SecItemCopyMatching failed with OSStatus {status}.");
      } finally {
        CFRelease(query);
      }
    } finally {
      CFRelease(cfService);
      CFRelease(cfAccount);
    }
  }

  /// <inheritdoc />
  public Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken) {
    if (!IsSupportedPlatform || _symbols.Value is not { } sym) {
      throw new PlatformNotSupportedException("macOS Keychain is only supported on macOS.");
    }

    cancellationToken.ThrowIfCancellationRequested();

    byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
    IntPtr cfService = CreateCFString(service);
    IntPtr cfAccount = CreateCFString(account);
    IntPtr cfSecretData = CFDataCreate(IntPtr.Zero, secretBytes, secretBytes.Length);
    try {
      IntPtr[] queryKeys = [sym.SecClass, sym.SecAttrService, sym.SecAttrAccount];
      IntPtr[] queryValues = [sym.SecClassGenericPassword, cfService, cfAccount];
      IntPtr query = CFDictionaryCreate(IntPtr.Zero, queryKeys, queryValues, queryKeys.Length, sym.KeyCallbacks, sym.ValueCallbacks);
      try {
        IntPtr[] updateKeys = [sym.SecValueData];
        IntPtr[] updateValues = [cfSecretData];
        IntPtr updateDict = CFDictionaryCreate(IntPtr.Zero, updateKeys, updateValues, updateKeys.Length, sym.KeyCallbacks, sym.ValueCallbacks);
        try {
          int status = SecItemUpdate(query, updateDict);
          if (status == ErrSecSuccess) {
            return Task.CompletedTask;
          }

          if (status == ErrSecItemNotFound) {
            IntPtr[] addKeys = [sym.SecClass, sym.SecAttrService, sym.SecAttrAccount, sym.SecValueData];
            IntPtr[] addValues = [sym.SecClassGenericPassword, cfService, cfAccount, cfSecretData];
            IntPtr addDict = CFDictionaryCreate(IntPtr.Zero, addKeys, addValues, addKeys.Length, sym.KeyCallbacks, sym.ValueCallbacks);
            try {
              int addStatus = SecItemAdd(addDict, out _);
              if (addStatus != ErrSecSuccess) {
                throw new InvalidOperationException($"SecItemAdd failed with OSStatus {addStatus}.");
              }
              return Task.CompletedTask;
            } finally {
              CFRelease(addDict);
            }
          }

          throw new InvalidOperationException($"SecItemUpdate failed with OSStatus {status}.");
        } finally {
          CFRelease(updateDict);
        }
      } finally {
        CFRelease(query);
      }
    } finally {
      CFRelease(cfSecretData);
      CFRelease(cfService);
      CFRelease(cfAccount);
    }
  }

  /// <inheritdoc />
  public Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken) {
    if (!IsSupportedPlatform || _symbols.Value is not { } sym) {
      return Task.CompletedTask;
    }

    cancellationToken.ThrowIfCancellationRequested();

    IntPtr cfService = CreateCFString(service);
    IntPtr cfAccount = CreateCFString(account);
    try {
      IntPtr[] queryKeys = [sym.SecClass, sym.SecAttrService, sym.SecAttrAccount];
      IntPtr[] queryValues = [sym.SecClassGenericPassword, cfService, cfAccount];
      IntPtr query = CFDictionaryCreate(IntPtr.Zero, queryKeys, queryValues, queryKeys.Length, sym.KeyCallbacks, sym.ValueCallbacks);
      try {
        int status = SecItemDelete(query);
        if (status != ErrSecSuccess && status != ErrSecItemNotFound) {
          throw new InvalidOperationException($"SecItemDelete failed with OSStatus {status}.");
        }
        return Task.CompletedTask;
      } finally {
        CFRelease(query);
      }
    } finally {
      CFRelease(cfService);
      CFRelease(cfAccount);
    }
  }

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

  private static IntPtr CreateCFString(string str) => CFStringCreateWithCharacters(IntPtr.Zero, str, str.Length);

  private sealed class KeychainSymbols {
    public IntPtr KeyCallbacks { get; }
    public IntPtr ValueCallbacks { get; }
    public IntPtr CFBooleanTrue { get; }
    public IntPtr SecClass { get; }
    public IntPtr SecClassGenericPassword { get; }
    public IntPtr SecAttrService { get; }
    public IntPtr SecAttrAccount { get; }
    public IntPtr SecValueData { get; }
    public IntPtr SecReturnData { get; }
    public IntPtr SecMatchLimit { get; }
    public IntPtr SecMatchLimitOne { get; }

    private KeychainSymbols(
        IntPtr keyCallbacks,
        IntPtr valueCallbacks,
        IntPtr cfBooleanTrue,
        IntPtr secClass,
        IntPtr secClassGenericPassword,
        IntPtr secAttrService,
        IntPtr secAttrAccount,
        IntPtr secValueData,
        IntPtr secReturnData,
        IntPtr secMatchLimit,
        IntPtr secMatchLimitOne) {
      KeyCallbacks = keyCallbacks;
      ValueCallbacks = valueCallbacks;
      CFBooleanTrue = cfBooleanTrue;
      SecClass = secClass;
      SecClassGenericPassword = secClassGenericPassword;
      SecAttrService = secAttrService;
      SecAttrAccount = secAttrAccount;
      SecValueData = secValueData;
      SecReturnData = secReturnData;
      SecMatchLimit = secMatchLimit;
      SecMatchLimitOne = secMatchLimitOne;
    }

    public static KeychainSymbols Load() {
      IntPtr cfLib = NativeLibrary.Load(CoreFoundationFramework);
      IntPtr secLib = NativeLibrary.Load(SecurityFramework);

      IntPtr keyCallbacks = NativeLibrary.GetExport(cfLib, "kCFTypeDictionaryKeyCallBacks");
      IntPtr valueCallbacks = NativeLibrary.GetExport(cfLib, "kCFTypeDictionaryValueCallBacks");
      IntPtr cfBooleanTrue = Marshal.ReadIntPtr(NativeLibrary.GetExport(cfLib, "kCFBooleanTrue"));

      IntPtr secClass = Marshal.ReadIntPtr(NativeLibrary.GetExport(secLib, "kSecClass"));
      IntPtr secClassGenericPassword = Marshal.ReadIntPtr(NativeLibrary.GetExport(secLib, "kSecClassGenericPassword"));
      IntPtr secAttrService = Marshal.ReadIntPtr(NativeLibrary.GetExport(secLib, "kSecAttrService"));
      IntPtr secAttrAccount = Marshal.ReadIntPtr(NativeLibrary.GetExport(secLib, "kSecAttrAccount"));
      IntPtr secValueData = Marshal.ReadIntPtr(NativeLibrary.GetExport(secLib, "kSecValueData"));
      IntPtr secReturnData = Marshal.ReadIntPtr(NativeLibrary.GetExport(secLib, "kSecReturnData"));
      IntPtr secMatchLimit = Marshal.ReadIntPtr(NativeLibrary.GetExport(secLib, "kSecMatchLimit"));
      IntPtr secMatchLimitOne = Marshal.ReadIntPtr(NativeLibrary.GetExport(secLib, "kSecMatchLimitOne"));

      return new KeychainSymbols(
          keyCallbacks,
          valueCallbacks,
          cfBooleanTrue,
          secClass,
          secClassGenericPassword,
          secAttrService,
          secAttrAccount,
          secValueData,
          secReturnData,
          secMatchLimit,
          secMatchLimitOne);
    }
  }
}
