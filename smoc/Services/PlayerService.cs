using Smoc.Streaming;
using Smoc.Ui;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Codecs.FFMpeg;
using SoundFlow.Structs;
using Terminal.Gui.App;

namespace Smoc.Services;

public sealed class PlayerService : IDisposable
{
    private readonly MainWindow mainWindow;
    private readonly IStreamingClient streamingClient;
    private readonly MiniAudioEngine audioEngine;
    private readonly DeviceInfo playbackDeviceInfo;
    private readonly AudioPlaybackDevice playbackDevice;

    private readonly List<Song> playbackQueue;
    private int currentPlaybackIndex;
    private PlaybackState playbackState;
    private StreamPlaybackService? streamPlaybackService;
    private CancellationTokenSource? playbackCts;

    public event EventHandler<float>? VolumeChanged;
    public event EventHandler<Song>? SongChanged;
    public event EventHandler<PlaybackState>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler? QueueChanged;

    public PlaybackState PlaybackState => this.playbackState;
    public Song? CurrentSong => GetCurrentSong();
    public TimeSpan CurrentTime => this.streamPlaybackService?.Time ?? TimeSpan.Zero;
    public TimeSpan Duration => this.streamPlaybackService?.Duration ?? TimeSpan.Zero;
    public float Progress => this.streamPlaybackService?.Progress ?? 0;
    public IEnumerable<Song> GetCurrentPlaybackQueue() => playbackQueue.ToList();
    public int CurrentPlaybackIndex => currentPlaybackIndex;

    public float Volume
    {
        get => this.playbackDevice.MasterMixer.Volume;
        set
        {
            this.playbackDevice.MasterMixer.Volume = value;
            InvokeAppEvent(VolumeChanged, value);
        }
    }

    public PlayerService(MainWindow mainWindow, IStreamingClient streamingClient)
    {
        this.mainWindow = mainWindow;
        this.streamingClient = streamingClient;
        this.audioEngine = new MiniAudioEngine();
        audioEngine.RegisterCodecFactory(new FFmpegCodecFactory());
        audioEngine.UpdateAudioDevicesInfo();
        this.playbackDeviceInfo = audioEngine.PlaybackDevices.FirstOrDefault(x => x.IsDefault);
        this.playbackDevice = audioEngine.InitializePlaybackDevice(playbackDeviceInfo, AudioFormat.DvdHq);
        this.playbackDevice.Start();

        this.playbackQueue = new List<Song>();
        this.currentPlaybackIndex = 0;
        this.playbackState = PlaybackState.Stopped;
        this.streamPlaybackService = null;
    }

    public void QueueSong(Song song)
    {
        playbackQueue.Add(song);
        InvokeAppEvent(QueueChanged);
    }

    public void QueueSongs(IEnumerable<Song> songs)
    {
        playbackQueue.AddRange(songs);
        InvokeAppEvent(QueueChanged);
    }

    public async Task ChangeTrack(int index)
    {
        if (index < 0 || index >= playbackQueue.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        Stop();
        currentPlaybackIndex = index;
        await Play();
    }

    public void ClearPlaybackQueue()
    {
        playbackQueue.Clear();
        InvokeAppEvent(QueueChanged);
    }

    public async Task PlayPause()
    {
        switch (playbackState)
        {
            case PlaybackState.Playing:
                Pause();
                break;
            case PlaybackState.Paused:
                await Play();
                break;
        }
    }

    public async Task Play()
    {
        switch (playbackState)
        {
            case PlaybackState.Paused:
                streamPlaybackService?.Play();
                playbackState = PlaybackState.Playing;
                InvokeAppEvent(PlaybackStateChanged, playbackState);
                return;
            case PlaybackState.Stopped:
                if (playbackQueue.Count == 0)
                {
                    return;
                }

                streamPlaybackService?.Dispose();
                playbackState = PlaybackState.Playing;
                InvokeAppEvent(PlaybackStateChanged, playbackState);
                await PlayCurrentSong();
                return;
        }
    }

    public void Pause()
    {
        if (playbackState != PlaybackState.Playing)
        {
            return;
        }

        streamPlaybackService?.Pause();
        playbackState = PlaybackState.Paused;
        InvokeAppEvent(PlaybackStateChanged, playbackState);
    }

    public void Stop()
    {
        if (playbackState == PlaybackState.Stopped)
        {
            return;
        }

        playbackState = PlaybackState.Stopped;
        InvokeAppEvent(PlaybackStateChanged, playbackState);
        streamPlaybackService?.Dispose();
        streamPlaybackService = null;
    }

    private async void OnStreamEnded(object? sender, EventArgs e)
    {
        Logging.Debug($"Stream ended for {CurrentSong?.Title} ({CurrentSong?.Id}).");
        streamPlaybackService?.Dispose();
        streamPlaybackService = null;

        if (++currentPlaybackIndex >= playbackQueue.Count)
        {
            Logging.Debug($"Reached the end of the queue, stopping playback.");
            currentPlaybackIndex = 0;
            playbackState = PlaybackState.Stopped;
            InvokeAppEvent(PlaybackStateChanged, playbackState);
        }
        else
        {
            Logging.Debug($"Playing next song...");
            await PlayCurrentSong();
        }
    }

    public void Dispose()
    {
        Stop();
        playbackCts?.Cancel();
        playbackCts?.Dispose();
        playbackDevice.Dispose();
        audioEngine.Dispose();
    }

    private Song? GetCurrentSong()
    {
        if (currentPlaybackIndex >= playbackQueue.Count)
        {
            return null;
        }

        return playbackQueue[currentPlaybackIndex];
    }

    private async Task PlayCurrentSong()
    {
        if (GetCurrentSong() is not Song currentSong)
        {
            throw new InvalidOperationException("No song in queue");
        }

        // Cancel any previous playback setup
        playbackCts?.Cancel();
        playbackCts?.Dispose();
        playbackCts = new CancellationTokenSource();
        var token = playbackCts.Token;

        try
        {
            Logging.Debug($"Starting playback for {currentSong.Title} ({currentSong.Id})...");
            var songStream = await streamingClient.GetSongStreamAsync(currentSong.Id, token);

            if (token.IsCancellationRequested) return;

            Logging.Debug($"Received stream for {currentSong.Title} ({currentSong.Id}), decoding format...");

            var codec = songStream.Codec;
            if (codec.StartsWith("mp4a"))
            {
                codec = "m4a";
            }

            // Check again before expensive operations
            if (token.IsCancellationRequested) return;

            using var decoder = audioEngine.CreateDecoder(songStream.Stream, codec, AudioFormat.DvdHq);

            var format = new AudioFormat
            {
                Format = decoder.SampleFormat,
                Channels = decoder.Channels,
                SampleRate = decoder.SampleRate,
                Layout = AudioFormat.GetLayoutFromChannels(decoder.Channels)
            };
            Logging.Debug($"Decoded format for {currentSong.Title} ({currentSong.Id}): {format.Format}, {format.Channels}, {format.SampleRate}, {format.Layout}");

            // Final check before starting playback service
            if (token.IsCancellationRequested) return;

            streamPlaybackService = new StreamPlaybackService(audioEngine, playbackDevice, songStream.Stream, format);
            streamPlaybackService.StreamEnded += OnStreamEnded;
            streamPlaybackService.PositionChanged += (sender, args) => InvokeAppEvent(PositionChanged, args);
            streamPlaybackService.Play();
            InvokeAppEvent(SongChanged, currentSong);
        }
        catch (OperationCanceledException)
        {
            Logging.Debug($"Playback setup for {currentSong.Title} cancelled.");
        }
    }

    /// <summary>
    /// Invokes an event handler on the UI thread.
    /// </summary>
    /// <remarks>
    /// This is required because many events from the underlying SoundFlow playback system
    /// can be triggered for audio-specific threads and subscribers will expect all event
    /// handlers to marshal back to the UI thread.
    /// </remarks>
    /// <param name="eventHandler">The event handler to invoke.</param>
    private void InvokeAppEvent(EventHandler? eventHandler)
    {
        mainWindow.App?.Invoke(() => eventHandler?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// Invokes an event handler on the UI thread.
    /// </summary>
    /// <remarks>
    /// This is required because many events from the underlying SoundFlow playback system
    /// can be triggered for audio-specific threads and subscribers will expect all event
    /// handlers to marshal back to the UI thread.
    /// </remarks>
    /// <param name="eventHandler">The event handler to invoke.</param>
    /// <param name="args">The arguments to pass to the event handler.</param>
    private void InvokeAppEvent<T>(EventHandler<T>? eventHandler, T args)
    {
        mainWindow.App?.Invoke(() => eventHandler?.Invoke(this, args));
    }
}