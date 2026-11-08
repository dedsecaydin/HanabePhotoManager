using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.Platform;
using HanabePhotoManager.Core.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public class LocalPersonClustererTests
{
    [Fact]
    public void PortablePixelReader_DecodesAndScalesToBgra()
    {
        var root = System.IO.Path.Combine(@"D:\HanabePhoto\.artifacts", "cluster-pixels-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            var path = System.IO.Path.Combine(root, "red.png");
            using (var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(192, 96, new(255, 0, 0, 255)))
                SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, path);
            var pixels = PortableClusterPixelReader.Read(path);
            Assert.Equal(96, pixels.Width);
            Assert.Equal(48, pixels.Height);
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixels.Pixels.Take(4).ToArray());
        }
        finally { System.IO.Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MatchingSyntheticColorSamplesShareACluster()
    {
        var root = System.IO.Path.Combine(@"D:\HanabePhoto\.artifacts", "cluster-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            var paths = new[] { System.IO.Path.Combine(root, "one.jpg"), System.IO.Path.Combine(root, "two.jpg") };
            foreach (var path in paths) System.IO.File.WriteAllBytes(path, [1]);
            var groups = paths.Select(path => new MediaGroup(System.IO.Path.GetFileNameWithoutExtension(path), MediaCategory.Jpeg, new SourceMediaFile(path, 1, DateTimeOffset.UtcNow), [])).ToArray();
            var pixels = Enumerable.Repeat(new byte[] { 0, 0, 255, 255 }, 16 * 16).SelectMany(x => x).ToArray();
            var clusterer = new LocalPersonClusterer(_ => new BgraImagePixels(16, 16, pixels));
            var result = await clusterer.ClusterAsync(groups, null, CancellationToken.None);
            Assert.Equal(2, result.RecognizedImages);
            Assert.Equal(1, result.ClusterCount);
            Assert.Equal(result.LabelsByPrimaryPath[paths[0]], result.LabelsByPrimaryPath[paths[1]]);
        }
        finally { System.IO.Directory.Delete(root, true); }
    }
}
