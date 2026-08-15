using SoundFlow.Abstracts;

namespace Smoc.Services.Audio.SoundFlow;

/// <summary>
/// A SoundFlow audio modifier that applies soft-knee saturation to prevent harsh digital clipping.
/// </summary>
public sealed class SoftClipperModifier : SoundModifier {
  /// <inheritdoc/>
  public override string Name { get; set; } = "SoftClipper";

  /// <summary>
  /// Initializes a new instance of <see cref="SoftClipperModifier"/>.
  /// </summary>
  public SoftClipperModifier() {
  }

  /// <inheritdoc/>
  public override float ProcessSample(float sample, int channel) {
    return AudioMath.SoftClip(sample);
  }
}
