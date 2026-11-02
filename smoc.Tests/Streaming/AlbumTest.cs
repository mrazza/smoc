using Smoc.Streaming;
using smoc.Tests.TestInfra;

namespace smoc.Tests.Streaming;

public class AlbumTest {
  [Fact]
  public void Constructor_DefaultType_IsAlbum() {
    var artist = new Artist("art-1", "Artist");
    var album = new Album("alb-1", artist, "Album Title", [EntityTestFactory.GenerateAlbumCover()]);

    Assert.Equal(AlbumType.Album, album.Type);
    Assert.Null(album.ReleaseYear);
  }

  [Fact]
  public void Constructor_ExplicitTypeAndYear_SetsProperties() {
    var artist = new Artist("art-1", "Artist");
    var ep = new Album("alb-2", artist, "EP Title", [EntityTestFactory.GenerateAlbumCover()], 2023, AlbumType.Ep);
    var single = new Album("alb-3", artist, "Single Title", [EntityTestFactory.GenerateAlbumCover()], 2024, AlbumType.Single);

    Assert.Equal(AlbumType.Ep, ep.Type);
    Assert.Equal(2023, ep.ReleaseYear);
    Assert.Equal(AlbumType.Single, single.Type);
    Assert.Equal(2024, single.ReleaseYear);
  }
}
