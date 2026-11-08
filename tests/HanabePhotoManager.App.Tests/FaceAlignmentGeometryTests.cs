using System.Numerics;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class FaceAlignmentGeometryTests
{
    [Fact]
    public void TemplateLandmarksProduceIdentityAndDegenerateLandmarksAreRejected()
    {
        Vector2[] points = [new(38.2946f, 51.6963f), new(73.5318f, 51.5014f), new(56.0252f, 71.7366f), new(41.5493f, 92.3655f), new(70.7299f, 92.2041f)];
        var transform = FaceAlignmentGeometry.Estimate(points);
        Assert.Equal(1, transform.A, 6);
        Assert.Equal(0, transform.B, 6);
        Assert.Equal(0, transform.OffsetX, 6);
        Assert.Equal(0, transform.OffsetY, 6);
        Assert.Throws<System.IO.InvalidDataException>(() => FaceAlignmentGeometry.Estimate(Enumerable.Repeat(Vector2.Zero, 5).ToArray()));
    }
}
