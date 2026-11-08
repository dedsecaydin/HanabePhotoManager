using Avalonia.Media.Imaging;
using HanabePhotoManager.App.PixelArt;

namespace HanabePhotoManager.Mac.Services;

internal sealed class MacPixelArtRenderer : IPixelArtRenderer
{
    public object Load(string path) => new Bitmap(path);

    public object DownscaleToGrid(object source, int targetSize, out int width, out int height)
    {
        using var encoded = new MemoryStream();
        ((Bitmap)source).Save(encoded);
        var bytes = PortablePixelArtProcessor.Downscale(encoded.ToArray(), targetSize, out width, out height);
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }

    public void Export(object grid, string outputPath)
    {
        using var encoded = new MemoryStream();
        ((Bitmap)grid).Save(encoded);
        File.WriteAllBytes(outputPath, PortablePixelArtProcessor.Export(encoded.ToArray()));
    }
}
