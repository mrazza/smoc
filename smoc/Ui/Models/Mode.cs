namespace Smoc.Ui.Models;

/// <summary>
/// Represents the operating mode of the application interface.
/// </summary>
public enum Mode {
  /// <summary>
  /// The Playback Queue view.
  /// </summary>
  [DisplayName("QUEUE")]
  Queue,

  /// <summary>
  /// The Artist detail view.
  /// </summary>
  [DisplayName("ARTIST")]
  Artist,

  /// <summary>
  /// The Command input mode.
  /// </summary>
  [DisplayName("COMMAND")]
  Command,

  /// <summary>
  /// The Song / Track list view.
  /// </summary>
  [DisplayName("TRACK")]
  Song,

  /// <summary>
  /// The Playlist view.
  /// </summary>
  [DisplayName("PLAYLIST")]
  Playlist,

  /// <summary>
  /// The Now Playing view.
  /// </summary>
  [DisplayName("NOW PLAYING")]
  NowPlaying
}
