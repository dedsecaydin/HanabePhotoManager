using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.PixelArt;

/// <summary>Shared image sizing and raster processing for pixel-art previews and PNG export.</summary>
public static class PortablePixelArtProcessor
{
    public static (int Width, int Height) CalculateGridSize(int width, int height, int target)
    {
        if (width <= 0 || height <= 0 || target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
        var scale = Math.Min(1d, Math.Min((double)target / width, (double)target / height));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    public static int CalculateBlockSize(int width, int height) => Math.Max(1, (int)Math.Ceiling(1024d / Math.Max(width, height)));

    public static byte[] Downscale(byte[] encoded, int target, out int width, out int height)
    {
        using var image = Image.Load(encoded);
        var size = CalculateGridSize(image.Width, image.Height, target);
        (width, height) = size;
        image.Mutate(operation => operation.Resize(size.Width, size.Height, KnownResamplers.Bicubic));
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    public static byte[] Export(byte[] encoded)
    {
        using var image = Image.Load(encoded);
        var block = CalculateBlockSize(image.Width, image.Height);
        image.Mutate(operation => operation.Resize(checked(image.Width * block), checked(image.Height * block), KnownResamplers.NearestNeighbor));
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }
}
