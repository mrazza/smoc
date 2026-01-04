using Smoc.Streaming;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Structs;
using Terminal.Gui.App;

namespace Smoc.Services;

internal sealed class StreamPlaybackService : IDisposable
{
    private readonly MiniAudioEngine audioEngine;
    private readonly Stream songStream;
    private readonly AudioFormat audioFormat;
    private readonly AudioPlaybackDevice playbackDevice;
    private readonly AssetDataProvider streamDataProvider;
    private readonly SoundPlayer soundPlayer;

    public event EventHandler? StreamEnded;
    public event EventHandler<TimeSpan>? PositionChanged;

    public TimeSpan Duration => TimeSpan.FromSeconds(this.soundPlayer.Duration);
    public TimeSpan Time => TimeSpan.FromSeconds(this.soundPlayer.Time);
    public float Progress => this.soundPlayer.Time / this.soundPlayer.Duration;

    public StreamPlaybackService(MiniAudioEngine audioEngine, AudioPlaybackDevice playbackDevice, Stream songStream, AudioFormat audioFormat)
    {
        this.audioEngine = audioEngine;
        this.playbackDevice = playbackDevice;
        this.songStream = songStream;
        this.audioFormat = audioFormat;

        this.streamDataProvider = new AssetDataProvider(audioEngine, audioFormat, songStream);
        this.soundPlayer = new SoundPlayer(audioEngine, audioFormat, streamDataProvider);
        this.playbackDevice.MasterMixer.AddComponent(this.soundPlayer);
        this.streamDataProvider.PositionChanged += (sender, args) => this.PositionChanged?.Invoke(this, this.Time);
        this.soundPlayer.PlaybackEnded += (sender, args) => this.StreamEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Play()
    {
        this.soundPlayer.Play();
    }

    public void Pause()
    {
        this.soundPlayer.Pause();
    }

    public void Stop()
    {
        this.soundPlayer.Stop();
    }

    public void Dispose()
    {
        this.playbackDevice.MasterMixer.RemoveComponent(this.soundPlayer);
        this.soundPlayer.Dispose();
        this.streamDataProvider.Dispose();
        this.songStream.Dispose();
    }
}