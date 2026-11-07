using System.Text.Json.Nodes;
using Smoc.Configuration;
using Xunit;

namespace smoc.Tests.Configuration;

public class ConfigFileWriterTests : IDisposable {
  private readonly string _tempDir;
  private readonly string _configPath;
  private readonly ConfigFileWriter _writer;

  public ConfigFileWriterTests() {
    _tempDir = Path.Combine(Path.GetTempPath(), "smoc_config_tests_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(_tempDir);
    _configPath = Path.Combine(_tempDir, "config.json");
    _writer = new ConfigFileWriter(_configPath);
  }

  public void Dispose() {
    try {
      if (Directory.Exists(_tempDir)) {
        Directory.Delete(_tempDir, recursive: true);
      }
    } catch {
      // Best-effort cleanup
    }
  }

  [Fact]
  public async Task LoadAsync_ReturnsEmptyObject_WhenFileDoesNotExist() {
    JsonObject result = await _writer.LoadAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.Empty(result);
  }

  [Fact]
  public async Task LoadAsync_ParsesJsonWithCommentsAndTrailingCommas() {
    string jsonWithComments = """
      {
        // Subsonic server settings
        "SubsonicConfig": {
          "ServerHost": "example.com",
          "ServerPort": 443,
        },
        /* SoundCloud settings */
        "SoundCloudConfig": {
          "ClientId": "client123",
        },
      }
      """;
    await File.WriteAllTextAsync(_configPath, jsonWithComments, TestContext.Current.CancellationToken);

    JsonObject result = await _writer.LoadAsync(TestContext.Current.CancellationToken);

    Assert.NotNull(result["SubsonicConfig"]);
    Assert.Equal("example.com", result["SubsonicConfig"]?["ServerHost"]?.GetValue<string>());
    Assert.Equal(443, result["SubsonicConfig"]?["ServerPort"]?.GetValue<int>());
    Assert.NotNull(result["SoundCloudConfig"]);
    Assert.Equal("client123", result["SoundCloudConfig"]?["ClientId"]?.GetValue<string>());
  }

  [Fact]
  public async Task LoadAsync_ThrowsInvalidOperationException_WhenJsonIsMalformed() {
    string malformedJson = "{ unquoted_key: missing_value, ";
    await File.WriteAllTextAsync(_configPath, malformedJson, TestContext.Current.CancellationToken);

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _writer.LoadAsync(TestContext.Current.CancellationToken));
    Assert.Contains("Failed to parse configuration file", ex.Message);
  }

  [Fact]
  public async Task SaveAsync_WritesAtomicallyAndCreatesBackup() {
    await File.WriteAllTextAsync(_configPath, "{\"Initial\": \"Value\"}", TestContext.Current.CancellationToken);

    JsonObject newConfig = new() {
      ["Updated"] = "NewValue"
    };

    await _writer.SaveAsync(newConfig, backupOriginal: true, TestContext.Current.CancellationToken);

    // Verify main file updated
    string updatedContent = await File.ReadAllTextAsync(_configPath, TestContext.Current.CancellationToken);
    Assert.Contains("NewValue", updatedContent);
    Assert.DoesNotContain("Initial", updatedContent);

    // Verify backup created with original content
    string backupPath = _configPath + ".bak";
    Assert.True(File.Exists(backupPath));
    string backupContent = await File.ReadAllTextAsync(backupPath, TestContext.Current.CancellationToken);
    Assert.Contains("Initial", backupContent);
  }

  [Fact]
  public async Task UpdateAsync_ModifiesExistingSectionsWithoutWipingUnrelatedKeys() {
    string initialJson = """
      {
        "Theme": "Dracula",
        "KeyBindings": {
          "Quit": ":"
        },
        "SubsonicConfig": {
          "ServerHost": "old.host"
        }
      }
      """;
    await File.WriteAllTextAsync(_configPath, initialJson, TestContext.Current.CancellationToken);

    await _writer.UpdateAsync(root => {
      var sub = root["SubsonicConfig"] as JsonObject ?? new JsonObject();
      sub["ServerHost"] = "new.host";
      root["SubsonicConfig"] = sub;
    }, backupOriginal: false, TestContext.Current.CancellationToken);

    JsonObject reloaded = await _writer.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Dracula", reloaded["Theme"]?.GetValue<string>());
    Assert.Equal(":", reloaded["KeyBindings"]?["Quit"]?.GetValue<string>());
    Assert.Equal("new.host", reloaded["SubsonicConfig"]?["ServerHost"]?.GetValue<string>());
  }
}
