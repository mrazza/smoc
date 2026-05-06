using Smoc.Streaming.SoundCloud.Models;
using Smoc.Streaming.SoundCloud.Util;

namespace smoc.Tests.Streaming.SoundCloud;

public class SoundCloudMappingTest {
  [Fact]
  public void MapTrackToSong_ReturnsCorrectSong() {
    var user = new SoundCloudUser(123, "Test Artist", "http://avatar");
    var track = new SoundCloudTrack(456, "Test Track", 180000, "http://artwork-large.jpg", user, new SoundCloudMedia([]));

    var song = SoundCloudMapper.MapTrackToSong(track);

    Assert.Equal("456", song.Id);
    Assert.Equal("Test Track", song.Title);
    Assert.Equal(TimeSpan.FromSeconds(180), song.Duration);
    Assert.Equal("Test Artist", song.Artist.Name);
    Assert.Equal("123", song.Artist.Id);
    Assert.Equal("SoundCloud Uploads", song.Album.Name);
    Assert.Equal("sc-uploads-123", song.Album.Id);
    Assert.Single(song.Album.Covers);
    Assert.Equal("http://artwork-t500x500.jpg", song.Album.Covers.First().Url);
  }

  [Fact]
  public void MapTrackToSong_NoArtwork_ReturnsEmptyCovers() {
    var user = new SoundCloudUser(123, "Test Artist", "http://avatar");
    var track = new SoundCloudTrack(456, "Test Track", 180000, null, user, new SoundCloudMedia([]));

    var song = SoundCloudMapper.MapTrackToSong(track);

    Assert.Empty(song.Album.Covers);
  }
}