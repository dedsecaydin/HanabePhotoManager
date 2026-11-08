using HanabePhotoManager.App.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PortableFacePreprocessorTests
{
    [Fact]
    public void IdentityAlignmentPreservesPixelsAndTensorUsesRgbChannels()
    {
        using var input = new Image<Rgb24>(112, 112, new Rgb24(11, 42, 231));
        using var aligned = PortableFacePreprocessor.Align(input, new FaceAlignmentTransform(1, 0, 0, 0));
        Assert.Equal(input[50, 40], aligned[50, 40]);
        var tensor = PortableFacePreprocessor.ToDetectorTensor(aligned);
        Assert.Equal(11, tensor[0, 0, 40, 50]);
        Assert.Equal(42, tensor[0, 1, 40, 50]);
        Assert.Equal(231, tensor[0, 2, 40, 50]);
    }
}
