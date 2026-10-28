using System.IO;
using HanabePhotoManager.App.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MapThumbnailCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hanabe-map-cache-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CancellationIsNotReportedAsMissingThumbnail()
    {
        var cache = new MapThumbnailCache(Path.Combine(_root, "cache"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetUrlAsync("missing.jpg", cancellation.Token));
    }

    [Fact]
    public async Task ConcurrentRequestsPublishOneCompleteImageAndPreserveSource()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "source.png");
        using (var image = new Image<Rgba32>(300, 200)) await image.SaveAsPngAsync(source);
        var before = await File.ReadAllBytesAsync(source);
        var cache = new MapThumbnailCache(Path.Combine(_root, "cache"));
        var urls = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => cache.GetUrlAsync(source)));
        Assert.All(urls, url => Assert.NotNull(url));
        Assert.Single(urls.Distinct());
        var result = Assert.Single(Directory.GetFiles(cache.Directory));
        using var decoded = await Image.LoadAsync(result);
        Assert.Equal(220, decoded.Width);
        Assert.Equal(before, await File.ReadAllBytesAsync(source));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public async Task AnimatedSourceUsesFirstFrameWithoutChangingOriginal()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "animated.gif");
        using (var image = new Image<Rgba32>(300, 200, Color.Red))
        {
            using var later = new Image<Rgba32>(300, 200, Color.Blue);
            for (var i = 0; i < 20; i++) image.Frames.AddFrame(later.Frames.RootFrame);
            await image.SaveAsGifAsync(source);
        }
        var bytes = await File.ReadAllBytesAsync(source);
        var cache = new MapThumbnailCache(Path.Combine(_root, "cache"));
        Assert.NotNull(await cache.GetUrlAsync(source));
        using var result = await Image.LoadAsync<Rgba32>(Assert.Single(Directory.GetFiles(cache.Directory, "*.jpg")));
        Assert.Single(result.Frames);
        Assert.True(result[100, 100].R > 240 && result[100, 100].B < 10);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
    }

    [Fact]
    public async Task BudgetRemovesOnlyOwnedCacheEntries()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "source.png");
        using (var image = new Image<Rgba32>(300, 200)) await image.SaveAsPngAsync(source);
        var cache = new MapThumbnailCache(Path.Combine(_root, "cache"), maximumBytes: 1024 * 1024);
        Directory.CreateDirectory(cache.Directory);
        var old = Path.Combine(cache.Directory, new string('a', 24) + ".jpg");
        await File.WriteAllBytesAsync(old, new byte[1024 * 1024]);
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-1));
        var unrelated = Path.Combine(cache.Directory, "user.jpg");
        await File.WriteAllTextAsync(unrelated, "preserve");
        Assert.NotNull(await cache.GetUrlAsync(source));
        Assert.False(File.Exists(old));
        Assert.Equal("preserve", await File.ReadAllTextAsync(unrelated));
        Assert.Empty(Directory.GetFiles(cache.Directory, "*.tmp"));
    }
}
