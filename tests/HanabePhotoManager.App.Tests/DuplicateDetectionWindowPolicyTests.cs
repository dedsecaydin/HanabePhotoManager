using FluentAssertions;
using HanabePhotoManager.App.Services;
using System.Windows;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class DuplicateDetectionWindowPolicyTests
{
    [Theory]
    [InlineData(true, WindowState.Minimized, 2, true)]
    [InlineData(false, WindowState.Minimized, 2, false)]
    [InlineData(true, WindowState.Normal, 2, false)]
    [InlineData(true, WindowState.Minimized, 0, false)]
    public void ShouldRestore_OnlyForEnabledActionableMinimizedResult(bool enabled, WindowState state, int count, bool expected)
    {
        DuplicateDetectionWindowPolicy.ShouldRestore(enabled, state, count).Should().Be(expected);
    }
}
