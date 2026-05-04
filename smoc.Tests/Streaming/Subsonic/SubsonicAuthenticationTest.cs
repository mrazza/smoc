
using Smoc.Streaming.Subsonic;

namespace smoc.Tests.Streaming.Subsonic;

public class SubsonicAuthenticationTest {
  [Fact]
  public void GenerateToken_ReturnsValidTokenAndSalt() {
    string password = "testpassword";
    var (token, salt) = SubsonicAuthentication.GenerateToken(password);

    Assert.NotNull(token);
    Assert.NotNull(salt);
    Assert.Equal(10, salt.Length);
    Assert.Equal(32, token.Length); // MD5 hex string length
  }

  [Fact]
  public void GenerateToken_IsRepeatableForVerification() {
    // This is how a server would verify: md5(password + salt)
    string password = "secret_password";
    var (token, salt) = SubsonicAuthentication.GenerateToken(password);

    using var md5 = System.Security.Cryptography.MD5.Create();
    byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(password + salt);
    byte[] hashBytes = md5.ComputeHash(inputBytes);
    string expectedToken = string.Join("", hashBytes.Select(b => b.ToString("x2")));

    Assert.Equal(expectedToken, token);
  }
}
