using FluentAssertions;
using HanabePhotoManager.App.Compression;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImageCollageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-collage-{Guid.NewGuid():N}");

    [Fact]
    public async Task VerticalCollage_PreservesOriginalSizesOrderAndCentersNarrowImages()
    {
        var red = await CreateImageAsync("red.png", 20, 10, Color.Red);
        var blue = await CreateImageAsync("blue.png", 10, 30, Color.Blue);

        var result = await new ImageCollageService().ComposeAsync(
            [red, blue],
            new CollageOptions(_root, CollageOrientation.Vertical, TargetBytes: null),
            progress: null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        using var output = await Image.LoadAsync<Rgba32>(result.OutputPath!);
        output.Width.Should().Be(20);
        output.Height.Should().Be(40);
        output[10, 5].R.Should().BeGreaterThan(200);
        output[10, 25].B.Should().BeGreaterThan(200);
        output[1, 25].R.Should().BeGreaterThan(245);
        output[1, 25].G.Should().BeGreaterThan(245);
        output[1, 25].B.Should().BeGreaterThan(245);
    }

    [Fact]
    public async Task HorizontalCollage_PreservesOriginalSizesAndInputOrder()
    {
        var red = await CreateImageAsync("red-horizontal.png", 20, 10, Color.Red);
        var blue = await CreateImageAsync("blue-horizontal.png", 10, 30, Color.Blue);

        var result = await new ImageCollageService().ComposeAsync(
            [red, blue],
            new CollageOptions(_root, CollageOrientation.Horizontal, TargetBytes: null),
            progress: null,
            CancellationToken.None);

        using var output = await Image.LoadAsync<Rgba32>(result.OutputPath!);
        output.Width.Should().Be(30);
        output.Height.Should().Be(30);
        output[10, 15].R.Should().BeGreaterThan(200);
        output[25, 15].B.Should().BeGreaterThan(200);
    }

    [Fact]
    public async Task Collage_ObservesCancellationBeforeAllocatingTheCanvas()
    {
        var source = await CreateImageAsync("cancel.png", 20, 20, Color.Red);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => new ImageCollageService().ComposeAsync(
            [source],
            new CollageOptions(_root, CollageOrientation.Vertical, null),
            progress: null,
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private async Task<string> CreateImageAsync(string name, int width, int height, Color color)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        using var image = new Image<Rgba32>(width, height, color);
        await image.SaveAsPngAsync(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
