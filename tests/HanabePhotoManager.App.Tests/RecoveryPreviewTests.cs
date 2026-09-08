using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Recovery;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class RecoveryPreviewTests
{
    [Fact]
    public void RangeStreamCannotReadOrSeekIntoAdjacentMedia()
    {
        using var image = new MemoryStream([1, 2, 3, 4, 5, 6]);
        using var range = new RecoveryPreviewService.CandidateStream(image, 2, 2, default);
        byte[] buffer = new byte[6];
        range.Read(buffer, 0, 6).Should().Be(2);
        buffer.Take(2).Should().Equal(3, 4);
        range.Read(buffer, 0, 6).Should().Be(0);
        Action seek = () => range.Seek(3, SeekOrigin.Begin);
        seek.Should().Throw<IOException>();
        Action write = () => range.Write(buffer, 0, 1);
        write.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void RangeStreamHonorsCancellationBeforeRead()
    {
        using var image = new MemoryStream([1, 2, 3]);
        using var cancel = new CancellationTokenSource();
        using var range = new RecoveryPreviewService.CandidateStream(image, 0, 3, cancel.Token);
        cancel.Cancel();
        Action read = () => range.Read(new byte[1], 0, 1);
        read.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public async Task JpegPreviewDecodesPixelsAndLeavesImageUnchanged()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HanabeRecoveryPreview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var bitmap = new System.Drawing.Bitmap(32, 24);
            using var encoded = new MemoryStream();
            bitmap.Save(encoded, System.Drawing.Imaging.ImageFormat.Jpeg);
            byte[] original = new byte[512].Concat(encoded.ToArray()).Concat(new byte[128]).ToArray();
            var path = Path.Combine(directory, "card.img");
            await File.WriteAllBytesAsync(path, original);
            var scan = await new RecoveryImageService().ScanAsync(path, null, default);
            var preview = await RecoveryPreviewService.LoadAsync(path, scan.Candidates.Single(), default);
            preview.Image.Should().NotBeNull();
            preview.Image!.PixelWidth.Should().Be(32);
            preview.Image.PixelHeight.Should().Be(24);
            preview.Image.IsFrozen.Should().BeTrue();
            preview.Description.Should().Contain("像素解码通过");
            (await File.ReadAllBytesAsync(path)).Should().Equal(original);
        }
        finally { Directory.Delete(directory, true); }
    }
}
