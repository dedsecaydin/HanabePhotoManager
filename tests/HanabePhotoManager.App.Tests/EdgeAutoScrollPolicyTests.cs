using System.Windows;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class EdgeAutoScrollPolicyTests
{
    private readonly EdgeAutoScrollPolicy _policy = new(maximumDelta: 22);
    private readonly Size _viewport = new(800, 600);

    [Fact]
    public void Calculate_ReturnsZeroInsideSafeCenter()
    {
        _policy.Calculate(new Point(400, 300), _viewport).Should().Be(new Vector(0, 0));
    }

    [Theory]
    [InlineData(400, 4, 0, -1)]
    [InlineData(400, 596, 0, 1)]
    [InlineData(4, 300, -1, 0)]
    [InlineData(796, 300, 1, 0)]
    public void Calculate_PointsTowardTheNearestViewportEdge(
        double x, double y, int expectedXSign, int expectedYSign)
    {
        var delta = _policy.Calculate(new Point(x, y), _viewport);

        Math.Sign(delta.X).Should().Be(expectedXSign);
        Math.Sign(delta.Y).Should().Be(expectedYSign);
    }

    [Fact]
    public void Calculate_AcceleratesTowardEdgeAndStaysBounded()
    {
        var inner = _policy.Calculate(new Point(400, 40), _viewport).Y;
        var outer = _policy.Calculate(new Point(400, 2), _viewport).Y;
        var outside = _policy.Calculate(new Point(400, -100), _viewport).Y;

        Math.Abs(outer).Should().BeGreaterThan(Math.Abs(inner));
        Math.Abs(outside).Should().BeLessThanOrEqualTo(22);
    }
}
