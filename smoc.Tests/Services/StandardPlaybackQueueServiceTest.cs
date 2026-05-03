using Moq;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Services.Audio;
using Smoc.Streaming;

namespace smoc.Tests.Services;

public class StandardPlaybackQueueServiceTest {
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IAudioService> _mockAudioService;
  private readonly Mock<IStreamingClient> _mockStreamingClient;

  public StandardPlaybackQueueServiceTest() {
    _fakeMainWindow = new FakeMainWindow();
    _mockAudioService = new Mock<IAudioService>();
    _mockStreamingClient = new Mock<IStreamingClient>();
  }

  private StandardPlaybackQueueService NewStandardPlaybackQueue() =>
    new(_fakeMainWindow, _mockStreamingClient.Object, _mockAudioService.Object);

  [Fact]
  public void New_EmptyDefaults() {
    using var sut = NewStandardPlaybackQueue();
    Assert.Empty(sut.GetCurrentPlaybackQueue());
    Assert.Equal(0, sut.CurrentPlaybackIndex);
    Assert.Null(sut.CurrentSong);
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
    Assert.Equal(TimeSpan.Zero, sut.CurrentTime);
    Assert.Equal(TimeSpan.Zero, sut.Duration);
    Assert.Equal(0, sut.Progress);
  }

  [Fact]
  public void Volume_ReturnsAudioServiceVolume() {
    _mockAudioService.SetupGet((s) => s.Volume).Returns(0.5f).Verifiable(Times.AtLeastOnce());
    using var sut = NewStandardPlaybackQueue();
    Assert.Equal(0.5f, sut.Volume);
    _mockAudioService.Verify();
  }

  [Fact]
  public void Volume_SetsAudioServiceVolume() {
    _mockAudioService.SetupSet((s) => s.Volume = It.IsAny<float>()).Verifiable(Times.Once());
    using var sut = NewStandardPlaybackQueue();
    sut.Volume = 0.5f;
    _mockAudioService.Verify();
  }

  [Fact]
  public void Volume_GetSet_ReturnsNewValue() {
    _mockAudioService.SetupProperty((s) => s.Volume);
    _mockAudioService.Object.Volume = 0.1f;
    using var sut = NewStandardPlaybackQueue();
    Assert.Equal(0.1f, sut.Volume);
    sut.Volume = 0.5f;
    Assert.Equal(0.5f, sut.Volume);
  }

  [Fact]
  public void Volume_Set_InvokesEvent() {
    _mockAudioService.SetupSet((s) => s.Volume = It.IsAny<float>());
    using var sut = NewStandardPlaybackQueue();
    float? receivedVolume = null;
    sut.VolumeChanged += (sender, volume) => receivedVolume = volume;
    sut.Volume = 0.5f;
    Assert.Equal(0.5f, receivedVolume);
  }

  [Fact]
  public void QueueNext_EmptyQueue_AddsSong() {
    using var sut = NewStandardPlaybackQueue();
    Song song = EntityTestFactory.GenerateSong();
    sut.QueueNext(song);
    Assert.Equal([song], sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public void QueueNext_EmptyQueue_AddsSongs() {
    using var sut = NewStandardPlaybackQueue();
    Song[] songs = [EntityTestFactory.GenerateSong(postfix: "_0"), EntityTestFactory.GenerateSong(postfix: "_1")];
    sut.QueueNext(songs);
    Assert.Equal(songs, sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public void QueueNext_NonEmptyQueue_AddsSong() {
    using var sut = NewStandardPlaybackQueue();
    Song[] songs = [EntityTestFactory.GenerateSong(postfix: "_0"), EntityTestFactory.GenerateSong(postfix: "_1")];
    sut.QueueNext(songs);
    Song song = EntityTestFactory.GenerateSong(postfix: "_2");
    sut.QueueNext(song);
    Assert.Equal([songs[0], song, songs[1]], sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public void QueueNext_NonEmptyQueue_InsertsSongs() {
    using var sut = NewStandardPlaybackQueue();
    Song[] songs = [EntityTestFactory.GenerateSong(postfix: "_0"), EntityTestFactory.GenerateSong(postfix: "_1")];
    sut.QueueNext(songs);
    Song[] songs2 = [EntityTestFactory.GenerateSong(postfix: "_2"), EntityTestFactory.GenerateSong(postfix: "_3")];
    sut.QueueNext(songs2);
    Assert.Equal([songs[0], .. songs2, songs[1]], sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public async Task QueueNext_NonEmptyQueueAtEnd_InsertsSongs() {
    using var sut = NewStandardPlaybackQueue();
    Song[] songs = [EntityTestFactory.GenerateSong(postfix: "_0"), EntityTestFactory.GenerateSong(postfix: "_1")];
    sut.QueueNext(songs);
    await sut.ChangeTrack(1);
    Song[] songs2 = [EntityTestFactory.GenerateSong(postfix: "_2"), EntityTestFactory.GenerateSong(postfix: "_3")];
    sut.QueueNext(songs2);
    Assert.Equal([.. songs, .. songs2], sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public void QueueNext_InvokesEvent() {
    bool eventReceived = false;
    using var sut = NewStandardPlaybackQueue();
    sut.QueueChanged += (_, __) => eventReceived = true;
    sut.QueueNext([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    Assert.True(eventReceived);
  }

  [Fact]
  public void QueueNext_WithEmptySongs_DoesNothing() {
    bool eventReceived = false;
    using var sut = NewStandardPlaybackQueue();
    sut.QueueChanged += (_, __) => eventReceived = true;
    sut.QueueNext([]);
    Assert.Empty(sut.GetCurrentPlaybackQueue());
    Assert.False(eventReceived);
  }

  [Fact]
  public void QueueNext_EmptyQueue_InvokesSongChangedEvent() {
    Song? songReceived = null;
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    sut.SongChanged += (_, song) => songReceived = song;
    sut.QueueNext([song]);
    Assert.Equal(song, songReceived);
  }

  [Fact]
  public void QueueLast_EmptyQueue_AddsSong() {
    using var sut = NewStandardPlaybackQueue();
    Song song = EntityTestFactory.GenerateSong();
    sut.QueueLast(song);
    Assert.Equal([song], sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public void QueueLast_EmptyQueue_AddsSongs() {
    using var sut = NewStandardPlaybackQueue();
    Song[] songs = [EntityTestFactory.GenerateSong(postfix: "_0"), EntityTestFactory.GenerateSong(postfix: "_1")];
    sut.QueueLast(songs);
    Assert.Equal(songs, sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public void QueueLast_NonEmptyQueue_AddsSong() {
    using var sut = NewStandardPlaybackQueue();
    Song song0 = EntityTestFactory.GenerateSong(postfix: "_0");
    Song song1 = EntityTestFactory.GenerateSong(postfix: "_1");
    sut.QueueLast(song0);
    sut.QueueLast(song1);
    Assert.Equal([song0, song1], sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public void QueueLast_NonEmptyQueue_AppendsSongs() {
    using var sut = NewStandardPlaybackQueue();
    Song[] songs = [EntityTestFactory.GenerateSong(postfix: "_0"), EntityTestFactory.GenerateSong(postfix: "_1")];
    sut.QueueNext(songs);
    Song[] songs2 = [EntityTestFactory.GenerateSong(postfix: "_2"), EntityTestFactory.GenerateSong(postfix: "_3")];
    sut.QueueLast(songs2);
    Assert.Equal([.. songs, .. songs2], sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public void QueueLast_InvokesEvent() {
    bool eventReceived = false;
    using var sut = NewStandardPlaybackQueue();
    sut.QueueChanged += (_, __) => eventReceived = true;
    sut.QueueLast([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    Assert.True(eventReceived);
  }

  [Fact]
  public void QueueLast_WithEmptySongs_DoesNothing() {
    bool eventReceived = false;
    using var sut = NewStandardPlaybackQueue();
    sut.QueueChanged += (_, __) => eventReceived = true;
    sut.QueueLast([]);
    Assert.Empty(sut.GetCurrentPlaybackQueue());
    Assert.False(eventReceived);
  }

  [Fact]
  public void QueueLast_EmptyQueue_InvokesSongChangedEvent() {
    Song? songReceived = null;
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    sut.SongChanged += (_, song) => songReceived = song;
    sut.QueueLast([song]);
    Assert.Equal(song, songReceived);
  }

  [Fact]
  public void ClearPlaybackQueue_ClearsQueue() {
    using var sut = NewStandardPlaybackQueue();
    Song[] songs = [EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()];
    sut.QueueNext(songs);
    sut.ClearPlaybackQueue();
    Assert.Empty(sut.GetCurrentPlaybackQueue());
  }

  [Fact]
  public void ClearPlaybackQueue_InvokesEvent() {
    var eventReceived = false;
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    sut.QueueChanged += (_, __) => eventReceived = true;
    sut.ClearPlaybackQueue();
    Assert.True(eventReceived);
  }

  [Fact]
  public void ClearPlaybackQueue_InvokesSongChangedEvent() {
    var eventReceived = false;
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    sut.SongChanged += (_, __) => eventReceived = true;
    sut.ClearPlaybackQueue();
    Assert.True(eventReceived);
  }

  [Fact]
  public async Task ChangeTrack_InvalidTrackIndex_TooLarge_ThrowsArgumentOutOfRangeException() {
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.ChangeTrack(2));
  }

  [Fact]
  public async Task ChangeTrack_InvalidTrackIndex_TooSmall_ThrowsArgumentOutOfRangeException() {
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.ChangeTrack(-1));
  }

  [Fact]
  public async Task ChangeTrack_ChangesTrackIndex() {
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    Assert.Equal(0, sut.CurrentPlaybackIndex);
    await sut.ChangeTrack(1);
    Assert.Equal(1, sut.CurrentPlaybackIndex);
  }

  [Fact]
  public async Task ChangeTrack_InvokesEvent() {
    bool eventReceived = false;
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    sut.SongChanged += (_, __) => eventReceived = true;
    await sut.ChangeTrack(1);
    Assert.True(eventReceived);
  }

  [Fact]
  public async Task ChangeTrack_ToCurrentTrack_DoesNothing() {
    bool eventReceived = false;
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    var originalIndex = sut.CurrentPlaybackIndex;
    sut.SongChanged += (_, __) => eventReceived = true;
    await sut.ChangeTrack(originalIndex);
    Assert.Equal(originalIndex, sut.CurrentPlaybackIndex);
    Assert.False(eventReceived);
  }

  [Fact]
  public async Task ChangeTrack_StopsAndResumesPlayback() {
    var song1 = EntityTestFactory.GenerateSong(id: "456", postfix: "_1");
    var song2 = EntityTestFactory.GenerateSong(id: "789", postfix: "_2");
    var fakePlayerService1 = new FakePlaybackService(song1);
    var fakePlayerService2 = new FakePlaybackService(song2);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1).Verifiable(Times.Once());
    _mockAudioService.Setup(a => a.MakePlaybackService(song2, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService2).Verifiable(Times.Once());
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream())).Verifiable(Times.Once());
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song2.Id, "m4a", new MemoryStream())).Verifiable(Times.Once());
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([song1, song2]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, fakePlayerService1.PlaybackState);
    Assert.Equal(PlaybackState.Stopped, fakePlayerService2.PlaybackState);
    await sut.ChangeTrack(1);
    Assert.Equal(PlaybackState.Stopped, fakePlayerService1.PlaybackState);
    Assert.Equal(PlaybackState.Playing, fakePlayerService2.PlaybackState);
    _mockAudioService.Verify();
    _mockStreamingClient.Verify();
  }

  [Fact]
  public async Task ChangeTrack_PlaybackStopped_RetainsStopped() {
    var song1 = EntityTestFactory.GenerateSong(id: "456", postfix: "_1");
    var song2 = EntityTestFactory.GenerateSong(id: "789", postfix: "_2");
    var fakePlayerService1 = new FakePlaybackService(song1);
    var fakePlayerService2 = new FakePlaybackService(song2);
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([song1, song2]);
    Assert.Equal(PlaybackState.Stopped, fakePlayerService1.PlaybackState);
    Assert.Equal(PlaybackState.Stopped, fakePlayerService2.PlaybackState);
    await sut.ChangeTrack(1);
    Assert.Equal(PlaybackState.Stopped, fakePlayerService1.PlaybackState);
    Assert.Equal(PlaybackState.Stopped, fakePlayerService2.PlaybackState);
  }

  [Fact]
  public void PlaybackState_NoSong_ReturnsStopped() {
    using var sut = NewStandardPlaybackQueue();
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public async Task PlaybackState_PlayerStopped_ReturnsStopped() {
    var song = EntityTestFactory.GenerateSong();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([song]);
    await sut.Play();
    sut.Stop();
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public async Task PlaybackState_PlayerPlaying_ReturnsPlaying() {
    var song = EntityTestFactory.GenerateSong();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([song]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
  }

  [Fact]
  public async Task PlaybackState_PlayerPaused_ReturnsPaused() {
    var song = EntityTestFactory.GenerateSong();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([song]);
    await sut.Play();
    sut.Pause();
    Assert.Equal(PlaybackState.Paused, sut.PlaybackState);
  }

  [Fact]
  public async Task PlaybackState_StateChanges_InvokesEvent() {
    var song = EntityTestFactory.GenerateSong();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([song]);
    PlaybackState? stateRecevied = null;
    sut.PlaybackStateChanged += (_, state) => stateRecevied = state;
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, stateRecevied);
  }

  [Fact]
  public void CurrentSong_NoSongs_ReturnsNull() {
    using var sut = NewStandardPlaybackQueue();
    Assert.Null(sut.CurrentSong);
  }

  [Fact]
  public void CurrentSong_OneSong_ReturnsSong() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([song]);
    Assert.Equal(song, sut.CurrentSong);
  }

  [Fact]
  public async Task CurrentSong_MultipleSongs_ReturnsCurrentSong() {
    var song1 = EntityTestFactory.GenerateSong(postfix: "_1");
    var song2 = EntityTestFactory.GenerateSong(postfix: "_2");
    using var sut = NewStandardPlaybackQueue();
    sut.QueueNext([song1, song2]);
    Assert.Equal(song1, sut.CurrentSong);
    await sut.ChangeTrack(1);
    Assert.Equal(song2, sut.CurrentSong);
  }

  [Fact]
  public void Duration_NoSong_ReturnsZero() {
    using var sut = NewStandardPlaybackQueue();
    Assert.Equal(TimeSpan.Zero, sut.Duration);
  }

  [Fact]
  public async Task Duration_SongPlaying_ReturnsCurrentSongDuration() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    Assert.Equal(song.Duration, sut.Duration);
  }

  [Fact]
  public void CurrentTime_NoSong_ReturnsZero() {
    using var sut = NewStandardPlaybackQueue();
    Assert.Equal(TimeSpan.Zero, sut.CurrentTime);
  }

  [Fact]
  public async Task CurrentTime_SongPlaying_ReturnsCurrentSongTime() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    fakePlayerService.SetCurrentTime(TimeSpan.FromSeconds(10));
    Assert.Equal(TimeSpan.FromSeconds(10), sut.CurrentTime);
  }

  [Fact]
  public void Progress_NoSong_ReturnsZero() {
    using var sut = NewStandardPlaybackQueue();
    Assert.Equal(0, sut.Progress);
  }

  [Fact]
  public async Task Progress_SongPlaying_ReturnsCurrentSongProgress() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    fakePlayerService.SetCurrentTime(TimeSpan.FromSeconds(30));
    Assert.Equal(0.1f, sut.Progress);
  }

  [Fact]
  public async Task Progress_SongPlaying_InvokesEvent() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    TimeSpan? positionReceived = null;
    sut.PositionChanged += (_, position) => positionReceived = position;
    fakePlayerService.SetCurrentTime(TimeSpan.FromSeconds(30));
    Assert.Equal(TimeSpan.FromSeconds(30), positionReceived);
  }

  [Fact]
  public async Task Play_NoSong_ThrowsInvalidOperationException() {
    using var sut = NewStandardPlaybackQueue();
    await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.Play());
  }

  [Fact]
  public async Task Play_ValidSongStopped_PlaysSong() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
  }

  [Fact]
  public async Task Play_ValidSongPaused_PlaysSong() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    sut.Pause();
    Assert.Equal(PlaybackState.Paused, sut.PlaybackState);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
  }

  [Fact]
  public async Task Play_ValidSongPlaying_DoesNothing() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
  }

  [Fact]
  public async Task Play_ValidSong_MapsMp4aToM4a() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "mp4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    _mockAudioService.Verify(a => a.MakePlaybackService(song, It.IsAny<Stream>(), "m4a", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Pause_ValidSongPlaying_PausesSong() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    sut.Pause();
    Assert.Equal(PlaybackState.Paused, sut.PlaybackState);
  }

  [Fact]
  public void Pause_NoSong_DoesNothing() {
    using var sut = NewStandardPlaybackQueue();
    sut.Pause();
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public async Task Pause_ValidSongPaused_DoesNothing() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    sut.Pause();
    Assert.Equal(PlaybackState.Paused, sut.PlaybackState);
    sut.Pause();
    Assert.Equal(PlaybackState.Paused, sut.PlaybackState);
  }

  [Fact]
  public async Task Stop_ValidSongPlaying_StopsSong() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    sut.Stop();
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public async Task Stop_ValidSongPaused_StopsSong() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    sut.Pause();
    Assert.Equal(PlaybackState.Paused, sut.PlaybackState);
    sut.Stop();
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public void Stop_NoSong_DoesNothing() {
    using var sut = NewStandardPlaybackQueue();
    sut.Stop();
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public async Task PlayPause_FromPlaying_Pauses() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    await sut.PlayPause();
    Assert.Equal(PlaybackState.Paused, sut.PlaybackState);
  }

  [Fact]
  public async Task PlayPause_FromPaused_Plays() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    sut.Pause();
    Assert.Equal(PlaybackState.Paused, sut.PlaybackState);
    await sut.PlayPause();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
  }

  [Fact]
  public async Task PlayPause_FromStopped_Plays() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
    await sut.PlayPause();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
  }

  [Fact]
  public async Task PlayPause_NoSong_ThrowsInvalidOperationException() {
    using var sut = NewStandardPlaybackQueue();
    await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.PlayPause());
  }

  [Fact]
  public async Task OnSongEnded_EndOfQueue_Stops() {
    var song = EntityTestFactory.GenerateSong();
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    fakePlayerService.EndSong();
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
    Assert.Equal(song, sut.CurrentSong);
  }

  [Fact]
  public async Task OnSongEnded_AdditionalSongsInQueue_PlaysNextSong() {
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var song2 = EntityTestFactory.GenerateSong(id: "2", postfix: "2");
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService1 = new FakePlaybackService(song1);
    var fakePlayerService2 = new FakePlaybackService(song2);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song2, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService2);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song2.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1, song2]);
    await sut.Play();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    Assert.Equal(song1, sut.CurrentSong);
    fakePlayerService1.EndSong();
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    Assert.Equal(song2, sut.CurrentSong);
  }

  [Fact]
  public async Task NextTrack_NotEndOfQueue_MovesToNextTrack() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var song2 = EntityTestFactory.GenerateSong(id: "2", postfix: "2");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    var fakePlayerService2 = new FakePlaybackService(song2);
    _mockAudioService.Setup(a => a.MakePlaybackService(song2, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService2);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song2.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1, song2]);
    await sut.Play();
    Assert.Equal(song1, sut.CurrentSong);
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    await sut.NextTrack();
    Assert.Equal(song2, sut.CurrentSong);
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
  }

  [Fact]
  public async Task NextTrack_EndOfQueue_StopsPlayback() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1]);
    await sut.Play();
    Assert.Equal(song1, sut.CurrentSong);
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    await sut.NextTrack();
    Assert.Equal(song1, sut.CurrentSong);
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public async Task NextTrack_NoQueue_DoesNothing() {
    using var sut = NewStandardPlaybackQueue();
    await sut.NextTrack();
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public async Task PreviousTrack_BeyondThreshold_RestartsSong() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1]);
    await sut.Play();
    fakePlayerService1.SetCurrentTime(TimeSpan.FromSeconds(30));
    Assert.Equal(song1, sut.CurrentSong);
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    Assert.Equal(TimeSpan.FromSeconds(30), fakePlayerService1.CurrentTime);
    await sut.PreviousTrack();
    Assert.Equal(song1, sut.CurrentSong);
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    Assert.Equal(TimeSpan.Zero, fakePlayerService1.CurrentTime);
  }

  [Fact]
  public async Task PreviousTrack_WithinThreshold_NoPreviousSong_StopsPlayback() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1]);
    await sut.Play();
    fakePlayerService1.SetCurrentTime(TimeSpan.FromSeconds(5));
    Assert.Equal(song1, sut.CurrentSong);
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    Assert.Equal(TimeSpan.FromSeconds(5), fakePlayerService1.CurrentTime);
    await sut.PreviousTrack();
    Assert.Equal(song1, sut.CurrentSong);
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public async Task PreviousTrack_WithinThreshold_PreviousSong_ChangesToPreviousSong() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var song2 = EntityTestFactory.GenerateSong(id: "2", postfix: "2");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    var fakePlayerService2 = new FakePlaybackService(song2);
    _mockAudioService.Setup(a => a.MakePlaybackService(song2, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService2);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song2.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1, song2]);
    await sut.Play();
    await sut.NextTrack();
    fakePlayerService1.SetCurrentTime(TimeSpan.FromSeconds(5));
    Assert.Equal(song2, sut.CurrentSong);
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
    Assert.Equal(TimeSpan.FromSeconds(5), fakePlayerService1.CurrentTime);
    await sut.PreviousTrack();
    Assert.Equal(song1, sut.CurrentSong);
    Assert.Equal(PlaybackState.Playing, sut.PlaybackState);
  }

  [Fact]
  public async Task PreviousTrack_NoQueue_DoesNothing() {
    using var sut = NewStandardPlaybackQueue();
    await sut.PreviousTrack();
    Assert.Equal(PlaybackState.Stopped, sut.PlaybackState);
  }

  [Fact]
  public void SeekTo_NoSong_DoesNothing() {
    using var sut = NewStandardPlaybackQueue();
    sut.SeekTo(TimeSpan.FromSeconds(30));
    Assert.Equal(TimeSpan.Zero, sut.CurrentTime);
  }

  [Fact]
  public async Task SeekTo_BeyondDuration_Throws() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1]);
    await sut.Play();
    var beforeTime = sut.CurrentTime;
    Assert.Throws<ArgumentOutOfRangeException>(() => sut.SeekTo(sut.Duration + TimeSpan.FromSeconds(10)));
    Assert.Equal(beforeTime, sut.CurrentTime);
  }

  [Fact]
  public async Task SeekTo_NewTime_Seeks() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1]);
    await sut.Play();
    fakePlayerService1.SetCurrentTime(TimeSpan.Zero);
    sut.SeekTo(TimeSpan.FromSeconds(30));
    Assert.Equal(TimeSpan.FromSeconds(30), sut.CurrentTime);
  }

  [Fact]
  public async Task SeekForward_Seeks() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1]);
    await sut.Play();
    fakePlayerService1.SetCurrentTime(TimeSpan.Zero);
    sut.SeekForward(TimeSpan.FromSeconds(30));
    Assert.Equal(TimeSpan.FromSeconds(30), sut.CurrentTime);
  }

  [Fact]
  public async Task SeekForward_BeyondDuration_HitsCeiling() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1]);
    await sut.Play();
    fakePlayerService1.SetCurrentTime(fakePlayerService1.Duration - TimeSpan.FromSeconds(15));
    sut.SeekForward(TimeSpan.FromSeconds(30));
    Assert.Equal(fakePlayerService1.Duration, sut.CurrentTime);
  }

  [Fact]
  public async Task SeekBackward_Seeks() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1]);
    await sut.Play();
    fakePlayerService1.SetCurrentTime(TimeSpan.FromMinutes(1));
    sut.SeekBackward(TimeSpan.FromSeconds(30));
    Assert.Equal(TimeSpan.FromSeconds(30), sut.CurrentTime);
  }

  [Fact]
  public async Task SeekBackward_BeyondStart_HitsFloor() {
    using var sut = NewStandardPlaybackQueue();
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var fakePlayerService1 = new FakePlaybackService(song1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song1]);
    await sut.Play();
    fakePlayerService1.SetCurrentTime(TimeSpan.FromSeconds(30));
    sut.SeekBackward(TimeSpan.FromSeconds(60));
    Assert.Equal(TimeSpan.Zero, sut.CurrentTime);
  }

  [Fact]
  public async Task Dispose_DisposesPlayerService() {
    var song = EntityTestFactory.GenerateSong();
    var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    _mockAudioService.Setup(a => a.MakePlaybackService(song, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song.Id, "m4a", new MemoryStream()));
    sut.QueueNext([song]);
    await sut.Play();
    sut.Dispose();
    Assert.True(fakePlayerService.IsDisposed);
  }

  [Fact]
  public void Dispose_DisposesAudioService() {
    var song = EntityTestFactory.GenerateSong();
    var sut = NewStandardPlaybackQueue();
    var fakePlayerService = new FakePlaybackService(song);
    sut.Dispose();
    _mockAudioService.Verify(a => a.Dispose(), Times.Once);
  }

  [Fact]
  public void Preload_QueueNext_StartsPreloadingNextTrack() {
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var song2 = EntityTestFactory.GenerateSong(id: "2", postfix: "2");
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService1 = new FakePlaybackService(song1);
    var fakePlayerService2 = new FakePlaybackService(song2);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song2, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService2);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song2.Id, "m4a", new MemoryStream()));

    sut.QueueNext([song1, song2]);

    _mockStreamingClient.Verify(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()), Times.Once);
    _mockAudioService.Verify(a => a.MakePlaybackService(song2, It.IsAny<Stream>(), "m4a", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Play_PreloadedSong_UsesPreloadedService() {
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var song2 = EntityTestFactory.GenerateSong(id: "2", postfix: "2");
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService1 = new FakePlaybackService(song1);
    var fakePlayerService2 = new FakePlaybackService(song2);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song2, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService2);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song2.Id, "m4a", new MemoryStream()));

    sut.QueueNext([song1, song2]);

    await sut.ChangeTrack(1);
    await sut.Play();

    Assert.Equal(PlaybackState.Playing, fakePlayerService2.PlaybackState);
    _mockStreamingClient.Verify(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task OnSongEnded_PreloadedTrackPlaysImmediately() {
    var song1 = EntityTestFactory.GenerateSong(id: "1", postfix: "1");
    var song2 = EntityTestFactory.GenerateSong(id: "2", postfix: "2");
    using var sut = NewStandardPlaybackQueue();
    var fakePlayerService1 = new FakePlaybackService(song1);
    var fakePlayerService2 = new FakePlaybackService(song2);
    _mockAudioService.Setup(a => a.MakePlaybackService(song1, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService1);
    _mockAudioService.Setup(a => a.MakePlaybackService(song2, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(fakePlayerService2);
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song1.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song1.Id, "m4a", new MemoryStream()));
    _mockStreamingClient.Setup(c => c.GetSongStreamAsync(song2.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SongStream(song2.Id, "m4a", new MemoryStream()));

    sut.QueueNext([song1, song2]);
    await sut.Play();

    fakePlayerService1.EndSong();

    Assert.Equal(PlaybackState.Playing, fakePlayerService2.PlaybackState);
  }
}