using System.Text.Json;
using System.Text.Json.Serialization;
using Smoc.Services.Audio;
using Terminal.Gui.App;

namespace Smoc.Services;

/// <summary>
/// JSON file-based implementation of <see cref="ISessionStateService"/> for persisting playback state across sessions.
/// </summary>
public sealed class JsonSessionStateService : ISessionStateService {
  private static readonly JsonSerializerOptions DefaultJsonOptions = new() {
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
  };

  private readonly string _filePath;
  private readonly JsonSerializerOptions _jsonOptions;

  /// <summary>
  /// Gets the path to the session state file.
  /// </summary>
  public string FilePath => _filePath;

  /// <summary>
  /// Initializes a new instance of the <see cref="JsonSessionStateService"/> class.
  /// </summary>
  /// <param name="filePath">
  /// Optional file path for storing session state. Defaults to <c>~/.config/smoc/session.json</c>.
  /// </param>
  /// <param name="jsonOptions">Optional JSON serializer options.</param>
  public JsonSessionStateService(string? filePath = null, JsonSerializerOptions? jsonOptions = null) {
    _filePath = filePath ?? Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      ".config",
      "smoc",
      "session.json");
    _jsonOptions = jsonOptions ?? DefaultJsonOptions;
  }

  /// <inheritdoc/>
  public void Save(SessionState state) {
    string tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
    try {
      string? directory = Path.GetDirectoryName(_filePath);
      if (!string.IsNullOrEmpty(directory)) {
        Directory.CreateDirectory(directory);
      }

      string json = JsonSerializer.Serialize(state, _jsonOptions);
      File.WriteAllText(tempPath, json);
      File.Move(tempPath, _filePath, overwrite: true);
    } catch (Exception ex) {
      Logging.Warning($"Failed to save session state: {ex.Message}");
      try {
        if (File.Exists(tempPath)) {
          File.Delete(tempPath);
        }
      } catch {
        // Suppress cleanup failure during error recovery.
      }
    }
  }

  /// <inheritdoc/>
  public SessionState? Load() {
    try {
      if (!File.Exists(_filePath)) {
        return null;
      }

      string json = File.ReadAllText(_filePath);
      SessionState? state = JsonSerializer.Deserialize<SessionState>(json, _jsonOptions);
      if (state == null || state.Queue == null) {
        return null;
      }

      int normalizedIndex = state.Queue.Count == 0
        ? 0
        : Math.Clamp(state.CurrentPlaybackIndex, 0, state.Queue.Count - 1);
      double normalizedPosition = Math.Max(
        0.0,
        double.IsNaN(state.CurrentPositionSeconds) || double.IsInfinity(state.CurrentPositionSeconds)
          ? 0.0
          : state.CurrentPositionSeconds);
      float normalizedVolume = Math.Clamp(
        float.IsNaN(state.Volume) || float.IsInfinity(state.Volume)
          ? AudioMath.DEFAULT_VOLUME
          : state.Volume,
        0f,
        2.0f);

      return state with {
        CurrentPlaybackIndex = normalizedIndex,
        CurrentPositionSeconds = normalizedPosition,
        Volume = normalizedVolume,
      };
    } catch (Exception ex) {
      Logging.Warning($"Failed to load session state: {ex.Message}");
      return null;
    }
  }
}
