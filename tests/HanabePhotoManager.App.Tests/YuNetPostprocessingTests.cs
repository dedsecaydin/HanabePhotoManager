using HanabePhotoManager.App.Services;
using System.Numerics;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class YuNetPostprocessingTests
{
    [Fact]
    public void SuppressionKeepsHighestScoreForOverlappingBoxes()
    {
        var faces = new[]
        {
            new YuNetDetection(0, 0, 100, 100, .9f, Array.Empty<Vector2>()),
            new YuNetDetection(5, 5, 100, 100, .8f, Array.Empty<Vector2>()),
            new YuNetDetection(300, 300, 40, 40, .7f, Array.Empty<Vector2>())
        };
        var result = YuNetDecoder.NonMaximumSuppression(faces, .3f);
        Assert.Equal(2, result.Count);
        Assert.Equal(.9f, result[0].Score);
        Assert.Equal(.7f, result[1].Score);
    }
}
