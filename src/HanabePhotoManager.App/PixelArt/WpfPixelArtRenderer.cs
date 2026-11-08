using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.PixelArt;

internal sealed class WpfPixelArtRenderer : IPixelArtRenderer
{
    public object Load(string path) => PixelArtRenderer.Load(path);
    public object DownscaleToGrid(object source, int targetSize, out int width, out int height) => PixelArtRenderer.DownscaleToGrid((BitmapSource)source, targetSize, out width, out height);
    public void Export(object grid, string outputPath) => PixelArtRenderer.Export((BitmapSource)grid, outputPath);
}
