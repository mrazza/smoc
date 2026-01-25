namespace Smoc.Ui.Models;

/// <summary>
/// Represents the operating mode of the application interface.
/// </summary>
public enum Mode {
  /// <summary>
  /// The Now Playing / Player view.
  /// </summary>
  [DisplayName("PLAYER")]
  Player,

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
  Playlist
}
