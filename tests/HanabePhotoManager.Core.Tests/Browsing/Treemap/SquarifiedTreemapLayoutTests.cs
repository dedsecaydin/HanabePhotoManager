using FluentAssertions;
using HanabePhotoManager.Core.Browsing.Treemap;

namespace HanabePhotoManager.Core.Tests.Browsing.Treemap;

public sealed class SquarifiedTreemapLayoutTests
{
    [Fact]
    public void Calculate_AllocatesAreaProportionally()
    {
        var nodes = new[]
        {
            new TreemapNode("large", "Large", 3, false),
            new TreemapNode("small", "Small", 1, false)
        };

        var tiles = new SquarifiedTreemapLayout().Calculate(
            nodes,
            new TreemapBounds(0, 0, 400, 100));

        tiles.Should().HaveCount(2);
        Area(tiles.Single(tile => tile.Node.Key == "large").Bounds)
            .Should().BeApproximately(30_000, 0.001);
        Area(tiles.Single(tile => tile.Node.Key == "small").Bounds)
            .Should().BeApproximately(10_000, 0.001);
    }

    [Fact]
    public void Calculate_UsesStablePathOrderForEqualWeights()
    {
        var nodes = new[]
        {
            new TreemapNode("z-path", "Z", 1, false),
            new TreemapNode("a-path", "A", 1, false),
            new TreemapNode("m-path", "M", 1, false)
        };

        var tiles = new SquarifiedTreemapLayout().Calculate(
            nodes,
            new TreemapBounds(0, 0, 300, 100));

        tiles.Select(tile => tile.Node.Key)
            .Should().Equal("a-path", "m-path", "z-path");
    }

    [Fact]
    public void Calculate_ExcludesNonPositiveAndNonFiniteWeights()
    {
        var nodes = new[]
        {
            new TreemapNode("valid", "Valid", 1, false),
            new TreemapNode("zero", "Zero", 0, false),
            new TreemapNode("negative", "Negative", -1, false),
            new TreemapNode("nan", "NaN", double.NaN, false),
            new TreemapNode("infinity", "Infinity", double.PositiveInfinity, false)
        };

        var tiles = new SquarifiedTreemapLayout().Calculate(
            nodes,
            new TreemapBounds(10, 20, 90, 60));

        tiles.Should().ContainSingle();
        tiles[0].Node.Key.Should().Be("valid");
        tiles[0].Bounds.Should().Be(new TreemapBounds(10, 20, 90, 60));
    }

    [Fact]
    public void Calculate_KeepsEveryTileInsideRequestedBounds()
    {
        var bounds = new TreemapBounds(5, 7, 997, 613);
        var nodes = Enumerable.Range(1, 1_000)
            .Select(index => new TreemapNode($"item-{index:D4}", $"Item {index}", index, false))
            .ToArray();

        var tiles = new SquarifiedTreemapLayout().Calculate(nodes, bounds);

        tiles.Should().HaveCount(1_000);
        tiles.Should().OnlyContain(tile =>
            tile.Bounds.X >= bounds.X - 0.001 &&
            tile.Bounds.Y >= bounds.Y - 0.001 &&
            tile.Bounds.Right <= bounds.Right + 0.001 &&
            tile.Bounds.Bottom <= bounds.Bottom + 0.001 &&
            tile.Bounds.Width >= 0 &&
            tile.Bounds.Height >= 0);
        tiles.Sum(tile => Area(tile.Bounds))
            .Should().BeApproximately(Area(bounds), 0.01);
    }

    [Fact]
    public void Calculate_ReturnsEmptyForEmptyInput()
    {
        new SquarifiedTreemapLayout()
            .Calculate([], new TreemapBounds(0, 0, 100, 100))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    public void Bounds_RejectNonPositiveExtent(double width, double height)
    {
        var act = () => new TreemapBounds(0, 0, width, height);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static double Area(TreemapBounds bounds) => bounds.Width * bounds.Height;
}
