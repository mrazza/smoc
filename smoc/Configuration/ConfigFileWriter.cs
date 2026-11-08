using System.Text.Json;
using System.Text.Json.Nodes;

namespace Smoc.Configuration;

/// <summary>
/// Provides safe and atomic reading and writing of JSON configuration files.
/// </summary>
/// <remarks>
/// While <see cref="LoadAsync"/> tolerates JSON comments and trailing commas, saving via
/// <see cref="SaveAsync"/> or <see cref="UpdateAsync"/> re-serializes the <see cref="JsonObject"/>
/// and does not preserve comments.
/// </remarks>
public sealed class ConfigFileWriter {
  private readonly string _configPath;

  /// <summary>
  /// Initializes a new instance of <see cref="ConfigFileWriter"/> targeting the specified path.
  /// </summary>
  /// <param name="configPath">Path to the target configuration file.</param>
  public ConfigFileWriter(string configPath) {
    _configPath = configPath;
  }

  /// <summary>
  /// Gets the path to the configuration file managed by this writer.
  /// </summary>
  public string ConfigPath => _configPath;

  /// <summary>
  /// Loads the configuration root node from disk, allowing comments and trailing commas.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>The parsed <see cref="JsonObject"/> or a new empty object if the file does not exist.</returns>
  /// <exception cref="InvalidOperationException">Thrown if the file contains invalid JSON.</exception>
  public async Task<JsonObject> LoadAsync(CancellationToken cancellationToken = default) {
    if (!File.Exists(_configPath)) {
      return new JsonObject();
    }

    string content = await File.ReadAllTextAsync(_configPath, cancellationToken);
    if (string.IsNullOrWhiteSpace(content)) {
      return new JsonObject();
    }

    JsonNodeOptions nodeOptions = new();
    JsonDocumentOptions documentOptions = new() {
      CommentHandling = JsonCommentHandling.Skip,
      AllowTrailingCommas = true
    };

    try {
      JsonNode? node = JsonNode.Parse(content, nodeOptions, documentOptions);
      if (node is JsonObject jsonObject) {
        return jsonObject;
      }
      throw new InvalidOperationException($"Configuration file at '{_configPath}' must contain a JSON object at its root.");
    } catch (JsonException ex) {
      throw new InvalidOperationException($"Failed to parse configuration file at '{_configPath}'. Fix syntax errors or remove the file: {ex.Message}", ex);
    }
  }

  /// <summary>
  /// Atomically writes a JSON configuration object to disk using a temporary file.
  /// </summary>
  /// <param name="node">The JSON node to write.</param>
  /// <param name="backupOriginal">Whether to create a .bak backup of an existing config file.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  public async Task SaveAsync(JsonObject node, bool backupOriginal = true, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(node);

    string? dir = Path.GetDirectoryName(_configPath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
      Directory.CreateDirectory(dir);
    }

    JsonSerializerOptions serializerOptions = new() { WriteIndented = true };
    string serialized = node.ToJsonString(serializerOptions);

    string tempPath = Path.Combine(dir ?? ".", $"{Path.GetFileName(_configPath)}.tmp.{Guid.NewGuid():N}");
    try {
      await File.WriteAllTextAsync(tempPath, serialized, cancellationToken);

      if (File.Exists(_configPath) && backupOriginal) {
        string backupPath = $"{_configPath}.bak";
        File.Copy(_configPath, backupPath, overwrite: true);
      }

      File.Move(tempPath, _configPath, overwrite: true);
    } finally {
      if (File.Exists(tempPath)) {
        try {
          File.Delete(tempPath);
        } catch {
          // Best effort cleanup
        }
      }
    }
  }

  /// <summary>
  /// Loads the configuration, executes an update action on the root object, and atomically saves the result.
  /// </summary>
  /// <param name="updater">The action that modifies the configuration object.</param>
  /// <param name="backupOriginal">Whether to create a backup file before writing.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  public async Task UpdateAsync(Action<JsonObject> updater, bool backupOriginal = true, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(updater);

    JsonObject rootNode = await LoadAsync(cancellationToken);
    updater(rootNode);
    await SaveAsync(rootNode, backupOriginal, cancellationToken);
  }
}
