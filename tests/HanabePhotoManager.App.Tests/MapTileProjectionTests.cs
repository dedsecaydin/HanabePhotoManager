using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MapTileProjectionTests
{
    [Fact]
    public void VisibleTilesCoverOnlyViewportAndWrapAcrossAntimeridian()
    {
        var tiles = MapViewport.VisibleTiles(0, 179, 2, 256, 256);
        Assert.NotEmpty(tiles);
        Assert.All(tiles, tile =>
        {
            Assert.InRange(tile.X, 0, 3);
            Assert.InRange(tile.Y, 0, 3);
            Assert.True(tile.Left < 256 && tile.Left + 256 > 0);
            Assert.True(tile.Top < 256 && tile.Top + 256 > 0);
        });
        Assert.Contains(tiles, tile => tile.X == 0);
        Assert.Contains(tiles, tile => tile.X == 3);
    }

    [Fact]
    public void EquatorAndMeridianAreAtTheCenterOfTheWorld()
    {
        var point = MapTileProjection.Project(0, 0, 2);
        Assert.Equal(2, point.X, 8);
        Assert.Equal(2, point.Y, 8);
        var inverse = MapTileProjection.Unproject(point.X, point.Y, 2);
        Assert.Equal(0, inverse.Latitude, 8);
        Assert.Equal(0, inverse.Longitude, 8);
    }

    [Fact]
    public void LongitudesWrapAndPolarLatitudesAreClamped()
    {
        var first = MapTileProjection.Project(90, 181, 3);
        var second = MapTileProjection.Project(85.05112878, -179, 3);
        Assert.Equal(first.X, second.X, 7);
        Assert.Equal(first.Y, second.Y, 7);
        Assert.InRange(first.Y, 0, 8);
        Assert.Throws<ArgumentOutOfRangeException>(() => MapTileProjection.Project(0, 0, 25));
    }
}
