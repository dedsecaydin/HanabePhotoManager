using FluentAssertions;
using HanabePhotoManager.App.Controls;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class AnimatedGifImageTests
{
    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 20)]
    [InlineData(2, 20)]
    [InlineData(9, 90)]
    public void NormalizeDelay_UsesSafeMinimum(int centiseconds, int milliseconds)
    {
        GifFrameTiming.NormalizeDelay(centiseconds).Should().Be(TimeSpan.FromMilliseconds(milliseconds));
    }
}
