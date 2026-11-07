using System.Runtime.InteropServices;
using System.Text;

namespace Smoc.Security.MacOs;

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

  [DllImport(SecurityFramework)]
  private static extern IntPtr SecClassCopyMatching();

  private static IntPtr CreateCFString(string str) => CFStringCreateWithCharacters(IntPtr.Zero, str, str.Length);

  public async Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken) {
    if (!IsSupportedPlatform) {
      return null;
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
          return Encoding.UTF8.GetString(buffer);
        } finally {
          CFRelease(resultPtr);
        }
      }

      return null;
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

  public async Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken) {
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

  public async Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken) {
    if (!IsSupportedPlatform) {
      return;
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
