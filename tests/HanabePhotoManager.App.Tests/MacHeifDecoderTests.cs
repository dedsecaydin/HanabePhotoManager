using HanabePhotoManager.Mac.Services;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacHeifDecoderTests
{
    [Fact]
    public void Decode_CleansUpItsTemporaryImageAndPreservesSource()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "mac-heif-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.heic");
            File.WriteAllText(source, "source");
            var decoder = new MacHeifDecoder(root, (_, output, _) =>
            {
                using var image = new Image<Rgb24>(4, 3);
                image.SaveAsPng(output);
            });
            using var decoded = decoder.Decode(source, CancellationToken.None);
            Assert.Equal(4, decoded.Width);
            Assert.Equal("source", File.ReadAllText(source));
            Assert.Single(Directory.GetFiles(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
