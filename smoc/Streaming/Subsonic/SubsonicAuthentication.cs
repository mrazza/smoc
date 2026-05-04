using System.Security.Cryptography;
using System.Text;

namespace Smoc.Streaming.Subsonic;

/// <summary>
/// Provides utility methods for Subsonic authentication, including token and salt generation.
/// </summary>
public static class SubsonicAuthentication {
  /// <summary>
  /// Generates a Subsonic-compatible authentication token and a random salt.
  /// </summary>
  /// <param name="password">The user's password.</param>
  /// <returns>A tuple containing the hex-encoded MD5 token (md5(password + salt)) and the random salt string.</returns>
  public static (string token, string salt) GenerateToken(string password) {
    string salt = Guid.NewGuid().ToString("n").Substring(0, 10);
    string input = password + salt;
    byte[] inputBytes = Encoding.UTF8.GetBytes(input);
    byte[] hashBytes = MD5.HashData(inputBytes);
    
    StringBuilder sb = new();
    for (int i = 0; i < hashBytes.Length; i++) {
      sb.Append(hashBytes[i].ToString("x2"));
    }
    
    return (sb.ToString(), salt);
  }
}
