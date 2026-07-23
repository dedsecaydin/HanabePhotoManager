using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PhotoViewportMathTests
{
    [Fact]
    public void AnchoredOffset_KeepsThePixelUnderThePointerStationary()
    {
        PhotoViewportMath.AnchoredOffset(1000, 2000, 200, 300, 1500).Should().Be(700);
    }

    [Theory]
    [InlineData(100, 60, 40)]
    [InlineData(100, -80, 180)]
    public void DragOffset_MovesContentWithThePointer(double startOffset, double pointerDelta, double expected)
    {
        PhotoViewportMath.DragOffset(startOffset, pointerDelta, 500).Should().Be(expected);
    }
}
