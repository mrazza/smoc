namespace Smoc.Configuration;

/// <summary>
/// Specifies how loudness normalization is applied.
/// </summary>
public enum LoudnessNormalizationMode {
  /// <summary>
  /// Only attenuates tracks louder than target LUFS; quiet tracks remain at unity gain.
  /// Matches standard YouTube web player behavior.
  /// </summary>
  AttenuateOnly,

  /// <summary>
  /// Attenuates loud tracks and boosts quiet tracks to meet target LUFS.
  /// </summary>
  Full
}
