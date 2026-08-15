using Smoc.Services.Audio;

namespace Smoc.Tests.Services.Audio;

public class AudioMathTest {
  [Fact]
  public void VolumeToGain_ZeroVolume_ReturnsZero() {
    Assert.Equal(0f, AudioMath.VolumeToGain(0f));
    Assert.Equal(0f, AudioMath.VolumeToGain(-0.5f));
  }

  [Fact]
  public void VolumeToGain_UnityVolume_ReturnsOne() {
    Assert.Equal(1.0f, AudioMath.VolumeToGain(1.0f));
  }

  [Fact]
  public void VolumeToGain_DefaultVolume_ReturnsExpectedGain() {
    Assert.Equal(0.64f, AudioMath.VolumeToGain(0.8f), 0.0001f);
  }

  [Fact]
  public void VolumeToGain_BoostVolume_ReturnsBoostGain() {
    Assert.Equal(1.5f, AudioMath.VolumeToGain(1.5f), 0.0001f);
    Assert.Equal(2.0f, AudioMath.VolumeToGain(2.0f), 0.0001f);
    Assert.Equal(2.0f, AudioMath.VolumeToGain(2.5f)); // clamped to 2.0
  }

  [Fact]
  public void VolumeToGain_IsMonotonicallyIncreasing() {
    float previousGain = -1f;
    for (float v = 0f; v <= 2.0f; v += 0.05f) {
      float gain = AudioMath.VolumeToGain(v);
      Assert.True(gain >= previousGain, $"Gain at volume {v} ({gain}) was not >= previous gain ({previousGain})");
      previousGain = gain;
    }
  }

  [Fact]
  public void GainToVolume_RoundTripsAccurately() {
    for (float v = 0f; v <= 2.0f; v += 0.1f) {
      float gain = AudioMath.VolumeToGain(v);
      float reconstructedVolume = AudioMath.GainToVolume(gain);
      Assert.Equal(v, reconstructedVolume, 0.001f);
    }
  }

  [Theory]
  [InlineData(0.0f)]
  [InlineData(0.2f)]
  [InlineData(-0.4f)]
  [InlineData(0.8f)]
  [InlineData(-0.8f)]
  public void SoftClip_BelowOrAtThreshold_IsBitExact(float sample) {
    Assert.Equal(sample, AudioMath.SoftClip(sample));
  }

  [Theory]
  [InlineData(0.9f)]
  [InlineData(1.0f)]
  [InlineData(1.5f)]
  [InlineData(2.0f)]
  [InlineData(10.0f)]
  public void SoftClip_AboveThreshold_IsBoundedAndSmooth(float sample) {
    float positiveResult = AudioMath.SoftClip(sample);
    float negativeResult = AudioMath.SoftClip(-sample);

    // Bounded strictly in (-1.0, 1.0)
    Assert.InRange(positiveResult, AudioMath.SOFT_CLIP_THRESHOLD, 1.0f);
    Assert.InRange(negativeResult, -1.0f, -AudioMath.SOFT_CLIP_THRESHOLD);

    // Symmetric
    Assert.Equal(-positiveResult, negativeResult, 0.00001f);
  }

  [Fact]
  public void SoftClipBuffer_ProcessesInPlace() {
    float[] buffer = [0.0f, 0.5f, 0.8f, 1.2f, -1.5f];
    AudioMath.SoftClipBuffer(buffer.AsSpan());

    Assert.Equal(0.0f, buffer[0]);
    Assert.Equal(0.5f, buffer[1]);
    Assert.Equal(0.8f, buffer[2]);
    Assert.InRange(buffer[3], AudioMath.SOFT_CLIP_THRESHOLD, 1.0f);
    Assert.InRange(buffer[4], -1.0f, -AudioMath.SOFT_CLIP_THRESHOLD);
  }

  [Theory]
  [InlineData(0.0f, 1.0f)]
  [InlineData(6.0205999f, 0.5f)]
  [InlineData(3.0f, 0.7079458f)]
  public void CalculateNormalizationGain_AttenuateOnly_PositiveLoudness_Attenuates(float loudnessDb, float expectedGain) {
    float gain = AudioMath.CalculateNormalizationGain(loudnessDb, attenuateOnly: true);
    Assert.Equal(expectedGain, gain, 0.001f);
  }

  [Theory]
  [InlineData(-3.0f, 1.0f)]
  [InlineData(-6.0f, 1.0f)]
  [InlineData(0.0f, 1.0f)]
  public void CalculateNormalizationGain_AttenuateOnly_NegativeOrZeroLoudness_ReturnsUnity(float loudnessDb, float expectedGain) {
    float gain = AudioMath.CalculateNormalizationGain(loudnessDb, attenuateOnly: true);
    Assert.Equal(expectedGain, gain, 0.001f);
  }

  [Theory]
  [InlineData(-6.0205999f, 2.0f)]
  [InlineData(-3.0f, 1.4125375f)]
  [InlineData(6.0205999f, 0.5f)]
  public void CalculateNormalizationGain_Full_ScalesBothDirections(float loudnessDb, float expectedGain) {
    float gain = AudioMath.CalculateNormalizationGain(loudnessDb, attenuateOnly: false);
    Assert.Equal(expectedGain, gain, 0.001f);
  }

  [Fact]
  public void CalculateNormalizationGain_InvalidValues_ReturnsUnity() {
    Assert.Equal(1.0f, AudioMath.CalculateNormalizationGain(float.NaN));
    Assert.Equal(1.0f, AudioMath.CalculateNormalizationGain(float.PositiveInfinity));
    Assert.Equal(1.0f, AudioMath.CalculateNormalizationGain(float.NegativeInfinity));
  }
}
