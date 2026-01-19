namespace Smoc.Ui.Models;

/// <summary>
/// Represents the operating mode of the application interface.
/// </summary>
public enum Mode {
  /// <summary>
  /// The Now Playing / Player view.
  /// </summary>
  [DisplayNameAttribute("PLAYER")]
  Player,

  /// <summary>
  /// The Artist detail view.
  /// </summary>
  [DisplayNameAttribute("ARTIST")]
  Artist,

  /// <summary>
  /// The Command input mode.
  /// </summary>
  [DisplayNameAttribute("COMMAND")]
  Command,

  /// <summary>
  /// The Song / Track list view.
  /// </summary>
  [DisplayNameAttribute("TRACK")]
  Song
}
