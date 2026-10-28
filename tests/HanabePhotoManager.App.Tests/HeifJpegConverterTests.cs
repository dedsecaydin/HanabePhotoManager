using System.IO;
using HanabePhotoManager.App.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class HeifJpegConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(@"D:\HanabePhoto\.artifacts\heif-converter-tests", Guid.NewGuid().ToString("N"));
    private string Original => Path.Combine(_root, "原片.HIF");
    public HeifJpegConverterTests()
    {
        Directory.CreateDirectory(_root);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "example.heic"), Original);
    }

    [Fact]
    public async Task PlannedCameraJpegName_IsReservedBeforeItExists()
    {
        var reserved = Path.ChangeExtension(Original, ".JPG");
        var generated = await new HeifJpegConverter().EnsureJpegAsync(Original, default,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { reserved });
        Assert.NotEqual(reserved, generated);
        Assert.False(File.Exists(reserved));
    }

    [Fact]
    public async Task ExistingJpeg_IsPreservedAndRestartReusesTheVerifiedConversion()
    {
        var collision = Path.ChangeExtension(Original, ".JPG");
        File.WriteAllBytes(collision, [42, 43]);
        var original = File.ReadAllBytes(Original);
        var generated = await new HeifJpegConverter().EnsureJpegAsync(Original, default);
        Assert.NotEqual(collision, generated);
        Assert.Equal(new byte[] { 42, 43 }, File.ReadAllBytes(collision));
        Assert.Equal(original, File.ReadAllBytes(Original));
        Assert.Equal(generated, await new HeifJpegConverter().EnsureJpegAsync(Original, default));
        Assert.Equal(2, Directory.GetFiles(_root, "*.JPG").Length);
    }

    [Fact]
    public async Task UserModifiedGeneratedJpeg_IsKeptAndANewCopyIsCreated()
    {
        var first = await new HeifJpegConverter().EnsureJpegAsync(Original, default);
        File.WriteAllBytes(first, [44, 45]);
        var second = await new HeifJpegConverter().EnsureJpegAsync(Original, default);
        Assert.NotEqual(first, second);
        Assert.Equal(new byte[] { 44, 45 }, File.ReadAllBytes(first));
        using var valid = SixLabors.ImageSharp.Image.Load(second);
        Assert.Equal(1280, valid.Width);
        Assert.Equal(854, valid.Height);
    }

    [Fact]
    public async Task CorruptHeif_ReportsFailureAndKeepsOriginalWithoutPublishingJpeg()
    {
        File.WriteAllBytes(Original, [1, 2, 3]);
        await Assert.ThrowsAsync<IOException>(() => new HeifJpegConverter().EnsureJpegAsync(Original, default));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Original));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task CanceledConversion_DoesNotPublishJpegOrReceipt()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new HeifJpegConverter().EnsureJpegAsync(Original, cancellation.Token));
        Assert.Single(Directory.GetFiles(_root));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
