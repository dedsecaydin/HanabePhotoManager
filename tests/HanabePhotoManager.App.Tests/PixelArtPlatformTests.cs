using HanabePhotoManager.App.PixelArt;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public class PixelArtPlatformTests
{
    [Fact]
    public void ViewModelUsesPlatformRendererForPreviewAndExport()
    {
        var directory = System.IO.Path.Combine(@"D:\HanabePhoto\.artifacts", "pixel-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var input = System.IO.Path.Combine(directory, "fixture.png");
        System.IO.File.WriteAllBytes(input, [1]);
        try
        {
            var renderer = new FakeRenderer();
            var model = new PixelArtViewModel(renderer);
            model.SetSourceImage(input);
            model.Generate();
            Assert.True(model.HasResult);
            Assert.Equal(64, model.PixelWidth);
            Assert.Equal(32, model.PixelHeight);
            model.Export(System.IO.Path.Combine(directory, "output.png"));
            Assert.True(renderer.Exported);
        }
        finally { System.IO.Directory.Delete(directory, true); }
    }

    private sealed class FakeRenderer : IPixelArtRenderer
    {
        public bool Exported { get; private set; }
        public object Load(string path) => new object();
        public object DownscaleToGrid(object source, int targetSize, out int width, out int height) { width = 64; height = 32; return new object(); }
        public void Export(object grid, string outputPath) => Exported = true;
    }
}
