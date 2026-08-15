namespace Smoc.Services.Audio;

/// <summary>
/// Provides mathematical utilities for audio volume curves and soft-clipping saturation.
/// </summary>
public static class AudioMath {
  /// <summary>
  /// The threshold at which soft-knee saturation begins (approx. -1.94 dBFS).
  /// Audio signals below this threshold remain bit-exact and undistorted.
  /// </summary>
  public const float SOFT_CLIP_THRESHOLD = 0.8f;

  /// <summary>
  /// Default volume level on application startup (80% / 0.8).
  /// </summary>
  public const float DEFAULT_VOLUME = 0.8f;

  /// <summary>
  /// Converts a user-facing volume value (0.0 to 2.0, corresponding to 0% - 200%)
  /// into a perceptual gain multiplier.
  /// </summary>
  /// <param name="volume">The logical volume level in the range [0.0, 2.0].</param>
  /// <returns>The linear gain multiplier.</returns>
  public static float VolumeToGain(float volume) {
    if (volume <= 0f) return 0f;
    if (volume <= 1.0f) {
      // Quadratic perceptual curve across 0.0 to 1.0 (100% = 1.0x unity gain)
      return volume * volume;
    }

    // Smooth linear boost above 100% up to 200% (2.0 = 2.0x gain / +6 dB)
    return Math.Clamp(1.0f + (volume - 1.0f), 1.0f, 2.0f);
  }

  /// <summary>
  /// Converts a linear gain multiplier back to a logical volume level.
  /// </summary>
  /// <param name="gain">The linear gain multiplier.</param>
  /// <returns>The logical volume level in the range [0.0, 2.0].</returns>
  public static float GainToVolume(float gain) {
    if (gain <= 0f) return 0f;
    if (gain <= 1.0f) {
      return MathF.Sqrt(gain);
    }
    return Math.Clamp(gain, 1.0f, 2.0f);
  }

  /// <summary>
  /// Applies a zero-latency soft-knee saturation curve to an audio sample.
  /// Samples below <see cref="SOFT_CLIP_THRESHOLD"/> pass through unchanged.
  /// Samples above the threshold smoothly saturate asymptotically towards +/- 1.0f.
  /// </summary>
  /// <param name="sample">The input PCM float sample.</param>
  /// <returns>The soft-clipped PCM sample bounded within [-1.0, 1.0].</returns>
  public static float SoftClip(float sample) {
    float abs = MathF.Abs(sample);
    if (abs <= SOFT_CLIP_THRESHOLD) {
      return sample;
    }

    float sign = MathF.Sign(sample);
    float margin = 1.0f - SOFT_CLIP_THRESHOLD;
    float saturated = SOFT_CLIP_THRESHOLD + margin * MathF.Tanh((abs - SOFT_CLIP_THRESHOLD) / margin);
    return sign * saturated;
  }

  /// <summary>
  /// Applies soft-clipping in-place across an entire buffer of audio samples.
  /// </summary>
  /// <param name="buffer">The span of audio samples to process.</param>
  public static void SoftClipBuffer(Span<float> buffer) {
    for (int i = 0; i < buffer.Length; i++) {
      buffer[i] = SoftClip(buffer[i]);
    }
  }

  /// <summary>
  /// Calculates the linear normalization gain multiplier for a given loudness in dB.
  /// </summary>
  /// <param name="loudnessDb">The loudness relative to the target in decibels (e.g. +3.0 dB for a hot track).</param>
  /// <param name="attenuateOnly">True to replicate web player behavior (only attenuate loud tracks, never boost).</param>
  /// <returns>The linear gain multiplier.</returns>
  public static float CalculateNormalizationGain(float loudnessDb, bool attenuateOnly = true) {
    if (float.IsNaN(loudnessDb) || float.IsInfinity(loudnessDb)) return 1.0f;
    if (attenuateOnly && loudnessDb <= 0f) return 1.0f;

    float linearGain = MathF.Pow(10f, -loudnessDb / 20f);
    return Math.Clamp(linearGain, 0.01f, 4.0f);
  }
}
