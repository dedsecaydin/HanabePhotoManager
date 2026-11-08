using HanabePhotoManager.App.Services;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public class LibraryHealthServiceTests
{
    [Fact]
    public void PortableDecoder_ValidatesARealImageAndRejectsInvalidData()
    {
        using var stream = new MemoryStream();
        using (var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(8, 8))
            SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, stream);
        stream.Position = 0;
        var decoder = new PortableImageHealthDecoder();
        var result = decoder.Decode(stream);
        Assert.False(result.HasMultipleFrames);
        Assert.False(result.ExceedsPixelLimit);
        Assert.Throws<InvalidDataException>(() => decoder.Decode(new MemoryStream([1, 2, 3])));
    }
    [Fact]
    public async Task ReportsDecodedAndEmptyFilesWithoutChangingThem()
    {
        var root = System.IO.Path.Combine(@"D:\HanabePhoto\.artifacts", "health-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            var good = System.IO.Path.Combine(root, "good.jpg");
            var empty = System.IO.Path.Combine(root, "empty.jpg");
            System.IO.File.WriteAllBytes(good, [1, 2, 3]);
            System.IO.File.WriteAllBytes(empty, []);
            var report = await new LibraryHealthService(new FakeDecoder()).CheckAsync([good, empty], true, null, CancellationToken.None);
            Assert.Equal(2, report.CheckedFiles);
            Assert.Equal(1, report.DecodedImages);
            Assert.Contains(report.Issues, issue => issue.Path == empty && issue.Kind == "空文件");
            Assert.Equal(new byte[] { 1, 2, 3 }, System.IO.File.ReadAllBytes(good));
        }
        finally { System.IO.Directory.Delete(root, true); }
    }

    private sealed class FakeDecoder : IImageHealthDecoder
    {
        public ImageHealthDecodeResult Decode(Stream stream) => new(false, false);
    }
}
