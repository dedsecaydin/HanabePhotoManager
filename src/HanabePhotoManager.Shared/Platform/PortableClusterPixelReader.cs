using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Platform;

/// <summary>Decode image pixels for the existing colour-histogram grouping workflow.</summary>
public static class PortableClusterPixelReader
{
    public static BgraImagePixels Read(string path)
    {
        using var image = Image.Load<Bgra32>(path);
        var height = Math.Max(1, (int)Math.Round(image.Height * 96d / image.Width));
        image.Mutate(context => context.Resize(96, height));
        var pixels = new byte[checked(image.Width * image.Height * 4)];
        image.CopyPixelDataTo(pixels);
        return new BgraImagePixels(image.Width, image.Height, pixels);
    }
}
