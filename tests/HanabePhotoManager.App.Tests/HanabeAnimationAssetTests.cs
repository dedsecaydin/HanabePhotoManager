using FluentAssertions;
using HanabePhotoManager.App.Services;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class HanabeAnimationAssetTests
{
    [Theory]
    [InlineData(HanabeAssistantVisualStyle.ChibiAnimated, HanabeAssistantState.Importing, "Assets/Hanabe/Animated/Chibi/importing.gif")]
    [InlineData(HanabeAssistantVisualStyle.PixelAnimated, HanabeAssistantState.Checking, "Assets/Hanabe/Animated/Pixel/checking.gif")]
    public void Resolver_ReturnsStyleAndStateSpecificResource(HanabeAssistantVisualStyle style, HanabeAssistantState state, string expected)
    {
        HanabeAssistantAnimationResolver.Resolve(style, state).Should().Be(expected);
    }

    [Fact]
    public void AllAnimatedAssets_AreEmbeddedMultiFrameGifs()
    {
        var root = FindRoot();
        var project = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "HanabePhotoManager.App.csproj"));
        foreach (var style in new[] { "Chibi", "Pixel" })
        foreach (var state in new[] { "idle", "scanning", "checking", "importing", "completed", "error" })
        {
            var path = Path.Combine(root, "src", "HanabePhotoManager.App", "Assets", "Hanabe", "Animated", style, state + ".gif");
            File.Exists(path).Should().BeTrue(path);
            File.ReadAllBytes(path).Take(6).Should().Equal("GIF89a".Select(c => (byte)c));
        }
        project.Should().Contain("Assets\\Hanabe\\Animated\\**\\*.gif");
    }

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HanabePhotoManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
