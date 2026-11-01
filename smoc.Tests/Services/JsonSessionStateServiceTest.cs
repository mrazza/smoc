using Smoc.Services;
using Smoc.Services.Audio;
using Smoc.Streaming;
using Xunit;

namespace smoc.Tests.Services;

/// <summary>
/// Unit tests for <see cref="JsonSessionStateService"/>.
/// </summary>
public sealed class JsonSessionStateServiceTest : IDisposable {
  private readonly string _testDirectory;
  private readonly string _sessionFilePath;

  /// <summary>
  /// Initializes a new instance of the <see cref="JsonSessionStateServiceTest"/> class.
  /// </summary>
  public JsonSessionStateServiceTest() {
    _testDirectory = Path.Combine(Path.GetTempPath(), $"smoc-session-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(_testDirectory);
    _sessionFilePath = Path.Combine(_testDirectory, "session.json");
  }

  /// <summary>
  /// Cleans up the temporary test directory.
  /// </summary>
  public void Dispose() {
    if (Directory.Exists(_testDirectory)) {
      try {
        Directory.Delete(_testDirectory, recursive: true);
      } catch {
        // Suppress cleanup exceptions in test teardown.
      }
    }
  }

  /// <summary>
  /// Verifies that <see cref="JsonSessionStateService.Load"/> returns null when the session file does not exist.
  /// </summary>
  [Fact]
  public void Load_WhenFileDoesNotExist_ReturnsNull() {
    var service = new JsonSessionStateService(_sessionFilePath);

    var result = service.Load();

    Assert.Null(result);
  }

  /// <summary>
  /// Verifies that saving and loading round-trips all session state properties accurately.
  /// </summary>
  [Fact]
  public void SaveAndLoad_RoundTripsQueueIndexPositionAndVolume() {
    var service = new JsonSessionStateService(_sessionFilePath);

    var artist = new Artist("art-1", "Radiohead");
    var albumCovers = new List<AlbumCover> {
      new("https://example.com/cover_sm.jpg", 128, 128),
      new("https://example.com/cover_lg.jpg", 512, 512),
    };
    var album = new Album("alb-1", artist, "OK Computer", albumCovers, 1997);
    var song1 = new Song("song-1", album, "Airbag", TimeSpan.FromSeconds(284), 1);
    var song2 = new Song("song-2", album, "Paranoid Android", TimeSpan.FromSeconds(383), 2);

    var originalState = new SessionState(
      Queue: [song1, song2],
      CurrentPlaybackIndex: 1,
      CurrentPositionSeconds: 124.5,
      Volume: 1.25f);

    service.Save(originalState);
    var loadedState = service.Load();

    Assert.NotNull(loadedState);
    Assert.Equal(originalState.CurrentPlaybackIndex, loadedState.CurrentPlaybackIndex);
    Assert.Equal(originalState.CurrentPositionSeconds, loadedState.CurrentPositionSeconds, precision: 3);
    Assert.Equal(originalState.Volume, loadedState.Volume, precision: 3);
    Assert.Equal(2, loadedState.Queue.Count);

    var loadedSong1 = loadedState.Queue[0];
    Assert.Equal("song-1", loadedSong1.Id);
    Assert.Equal("Airbag", loadedSong1.Title);
    Assert.Equal(TimeSpan.FromSeconds(284), loadedSong1.Duration);
    Assert.Equal(1, loadedSong1.TrackNumber);
    Assert.Equal("alb-1", loadedSong1.Album.Id);
    Assert.Equal("OK Computer", loadedSong1.Album.Name);
    Assert.Equal(1997, loadedSong1.Album.ReleaseYear);
    Assert.Equal("art-1", loadedSong1.Artist.Id);
    Assert.Equal("Radiohead", loadedSong1.Artist.Name);
    Assert.Equal("art-1", loadedSong1.Album.Artist.Id);
    Assert.Equal("Radiohead", loadedSong1.Album.Artist.Name);

    var loadedCovers = loadedSong1.Album.Covers.ToList();
    Assert.Equal(2, loadedCovers.Count);
    Assert.Equal("https://example.com/cover_sm.jpg", loadedCovers[0].Url);
    Assert.Equal(128, loadedCovers[0].Width);
    Assert.Equal(128, loadedCovers[0].Height);
    Assert.Equal("https://example.com/cover_lg.jpg", loadedCovers[1].Url);
    Assert.Equal(512, loadedCovers[1].Width);
    Assert.Equal(512, loadedCovers[1].Height);

    var loadedSong2 = loadedState.Queue[1];
    Assert.Equal("song-2", loadedSong2.Id);
    Assert.Equal("Paranoid Android", loadedSong2.Title);
    Assert.Equal(TimeSpan.FromSeconds(383), loadedSong2.Duration);
    Assert.Equal(2, loadedSong2.TrackNumber);
  }

  /// <summary>
  /// Verifies that loading from a file with corrupted JSON returns null without throwing an unhandled exception.
  /// </summary>
  [Fact]
  public void Load_WithCorruptedJson_ReturnsNullAndDoesNotThrow() {
    var service = new JsonSessionStateService(_sessionFilePath);
    File.WriteAllText(_sessionFilePath, "{ invalid json session content: true, ");

    var result = service.Load();

    Assert.Null(result);
  }

  /// <summary>
  /// Verifies that loading null JSON or JSON missing the Queue returns null.
  /// </summary>
  [Fact]
  public void Load_WithNullOrMissingQueue_ReturnsNull() {
    var service = new JsonSessionStateService(_sessionFilePath);

    File.WriteAllText(_sessionFilePath, "null");
    Assert.Null(service.Load());

    File.WriteAllText(_sessionFilePath, "{\"CurrentPlaybackIndex\": 0, \"CurrentPositionSeconds\": 0, \"Volume\": 1.0}");
    Assert.Null(service.Load());
  }

  /// <summary>
  /// Verifies that out-of-bounds playback index, position, and volume values are clamped to safe ranges on load.
  /// </summary>
  [Fact]
  public void Load_WithOutOfBoundsValues_ClampsIndexPositionAndVolume() {
    var service = new JsonSessionStateService(_sessionFilePath);

    var artist = new Artist("art-1", "Artist");
    var album = new Album("alb-1", artist, "Album", []);
    var song = new Song("song-1", album, "Title", TimeSpan.FromSeconds(100), 1);

    // Case 1: Out of bounds high (index > count-1, volume > 2.0)
    var stateHigh = new SessionState(
      Queue: [song],
      CurrentPlaybackIndex: 99,
      CurrentPositionSeconds: 50.0,
      Volume: 5.0f);
    service.Save(stateHigh);

    var loadedHigh = service.Load();
    Assert.NotNull(loadedHigh);
    Assert.Equal(0, loadedHigh.CurrentPlaybackIndex); // 1 song -> max index 0
    Assert.Equal(2.0f, loadedHigh.Volume);

    // Case 2: Out of bounds low (index < 0, position < 0, volume < 0)
    var stateLow = new SessionState(
      Queue: [song],
      CurrentPlaybackIndex: -5,
      CurrentPositionSeconds: -20.0,
      Volume: -1.0f);
    service.Save(stateLow);

    var loadedLow = service.Load();
    Assert.NotNull(loadedLow);
    Assert.Equal(0, loadedLow.CurrentPlaybackIndex);
    Assert.Equal(0.0, loadedLow.CurrentPositionSeconds);
    Assert.Equal(0.0f, loadedLow.Volume);

    // Case 3: Empty queue clamps index to 0
    var stateEmpty = new SessionState(
      Queue: [],
      CurrentPlaybackIndex: 10,
      CurrentPositionSeconds: 15.0,
      Volume: 1.0f);
    service.Save(stateEmpty);

    var loadedEmpty = service.Load();
    Assert.NotNull(loadedEmpty);
    Assert.Equal(0, loadedEmpty.CurrentPlaybackIndex);

    // Case 4: NaN and Infinity values
    var stateNan = new SessionState(
      Queue: [song],
      CurrentPlaybackIndex: 0,
      CurrentPositionSeconds: double.NaN,
      Volume: float.NaN);
    service.Save(stateNan);

    var loadedNan = service.Load();
    Assert.NotNull(loadedNan);
    Assert.Equal(0.0, loadedNan.CurrentPositionSeconds);
    Assert.Equal(AudioMath.DEFAULT_VOLUME, loadedNan.Volume);
  }

  /// <summary>
  /// Verifies that saving overwrites any existing session file atomically without leaving temporary files.
  /// </summary>
  [Fact]
  public void Save_OverwritesExistingSessionFileAtomically() {
    var service = new JsonSessionStateService(_sessionFilePath);

    var state1 = new SessionState(
      Queue: [],
      CurrentPlaybackIndex: 0,
      CurrentPositionSeconds: 10.0,
      Volume: 0.5f);
    service.Save(state1);

    var loaded1 = service.Load();
    Assert.NotNull(loaded1);
    Assert.Equal(0.5f, loaded1.Volume);

    var state2 = new SessionState(
      Queue: [],
      CurrentPlaybackIndex: 0,
      CurrentPositionSeconds: 25.0,
      Volume: 1.5f);
    service.Save(state2);

    var loaded2 = service.Load();
    Assert.NotNull(loaded2);
    Assert.Equal(1.5f, loaded2.Volume);
    Assert.Equal(25.0, loaded2.CurrentPositionSeconds);

    // Verify atomic move left no temporary files in directory
    var tmpFiles = Directory.GetFiles(_testDirectory, "*.tmp");
    Assert.Empty(tmpFiles);
  }

  /// <summary>
  /// Verifies that <see cref="JsonSessionStateService.Save"/> automatically creates parent directories when they do not exist.
  /// </summary>
  [Fact]
  public void Save_CreatesParentDirectoryIfMissing() {
    string nestedPath = Path.Combine(_testDirectory, "sub1", "sub2", "session.json");
    var service = new JsonSessionStateService(nestedPath);

    var state = new SessionState(
      Queue: [],
      CurrentPlaybackIndex: 0,
      CurrentPositionSeconds: 0.0,
      Volume: 1.0f);
    service.Save(state);

    Assert.True(File.Exists(nestedPath));
    var loaded = service.Load();
    Assert.NotNull(loaded);
  }

  /// <summary>
  /// Verifies default constructor initializes path to ~/.config/smoc/session.json.
  /// </summary>
  [Fact]
  public void Constructor_DefaultsToUserConfigDirectory() {
    var service = new JsonSessionStateService();

    string expectedSuffix = Path.Combine(".config", "smoc", "session.json");
    Assert.EndsWith(expectedSuffix, service.FilePath);
  }

  /// <summary>
  /// Verifies that <see cref="JsonSessionStateService.Save"/> throws <see cref="ArgumentNullException"/> when state is null.
  /// </summary>
  [Fact]
  public void Save_NullState_ThrowsArgumentNullException() {
    var sut = new JsonSessionStateService(_sessionFilePath);

    Assert.Throws<ArgumentNullException>(() => sut.Save(null!));
  }
}
