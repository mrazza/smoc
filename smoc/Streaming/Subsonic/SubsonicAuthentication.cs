
using System.Security.Cryptography;
using System.Text;

namespace Smoc.Streaming.Subsonic;

public static class SubsonicAuthentication {
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
