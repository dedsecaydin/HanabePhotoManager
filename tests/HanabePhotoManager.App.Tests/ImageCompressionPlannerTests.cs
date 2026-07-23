using FluentAssertions;
using HanabePhotoManager.App.Compression;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImageCompressionPlannerTests
{
    [Fact]
    public void CreatePlan_PerImage_AssignsTheSameCeilingToEveryFile()
    {
        var files = new[] { new CompressionSource("a.jpg", 900, 100), new CompressionSource("b.jpg", 100, 100) };

        var plan = new ImageCompressionPlanner().CreatePlan(files, CompressionTargetMode.PerImage, 250);

        plan.Select(item => item.TargetBytes).Should().Equal(250, 250);
    }

    [Fact]
    public void CreatePlan_WholeBatch_DistributesAllBytesProportionallyAndDeterministically()
    {
        var files = new[] { new CompressionSource("a.jpg", 300, 100), new CompressionSource("b.jpg", 100, 100) };

        var plan = new ImageCompressionPlanner().CreatePlan(files, CompressionTargetMode.WholeBatch, 101);

        plan.Sum(item => item.TargetBytes).Should().Be(101);
        plan[0].TargetBytes.Should().Be(76);
        plan[1].TargetBytes.Should().Be(25);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreatePlan_RejectsNonPositiveTargets(long target)
    {
        var act = () => new ImageCompressionPlanner().CreatePlan(
            [new CompressionSource("a.jpg", 1, 1)], CompressionTargetMode.PerImage, target);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
