using System.Windows;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class HanabeAssistantWindowPolicyTests
{
    [Theory]
    [InlineData(WindowState.Minimized, true, true)]
    [InlineData(WindowState.Minimized, false, false)]
    [InlineData(WindowState.Normal, true, false)]
    [InlineData(WindowState.Maximized, true, false)]
    public void ShouldShow_OnlyWhenMinimizedAndEnabled(WindowState state, bool enabled, bool expected)
    {
        HanabeAssistantWindowPolicy.ShouldShow(state, enabled).Should().Be(expected);
    }
}
