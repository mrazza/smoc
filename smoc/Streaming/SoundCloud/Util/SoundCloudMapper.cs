using Smoc.Streaming.SoundCloud.Models;

namespace Smoc.Streaming.SoundCloud.Util;

public static class SoundCloudMapper {
  public static Smoc.Streaming.Song MapTrackToSong(SoundCloudTrack track) {
    var artist = new Smoc.Streaming.Artist(track.User.Id.ToString(), track.User.Username);
    var album = new Smoc.Streaming.Album($"sc-uploads-{track.User.Id}", artist, "SoundCloud Uploads", 
        string.IsNullOrEmpty(track.ArtworkUrl) ? [] : [new Smoc.Streaming.AlbumCover(track.ArtworkUrl.Replace("-large", "-t500x500"), 500, 500)]);
    
    return new Smoc.Streaming.Song(track.Id.ToString(), album, track.Title, TimeSpan.FromMilliseconds(track.Duration));
  }
}