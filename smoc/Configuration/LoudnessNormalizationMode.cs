namespace Smoc.Configuration;

/// <summary>
/// Specifies how loudness normalization is applied.
/// </summary>
public enum LoudnessNormalizationMode {
  /// <summary>
  /// Attenuates loud tracks and boosts quiet tracks to meet target LUFS.
  /// Matches standard normalization in Spotify and Tidal desktop clients using peak limiting.
  /// </summary>
  Full,

  /// <summary>
  /// Only attenuates tracks louder than target LUFS; quiet tracks remain at unity gain.
  /// Matches YouTube web player behavior to avoid boosting uncompressed tracks without a limiter.
  /// </summary>
  AttenuateOnly
}
