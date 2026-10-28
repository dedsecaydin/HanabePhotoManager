using HanabePhotoManager.Core.Collage;
using Size = SixLabors.ImageSharp.Size;
using Rectangle = SixLabors.ImageSharp.Rectangle;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
namespace HanabePhotoManager.App.Collage;
internal static class CollageImagePlacement
{
    internal static void Resize(Image<Rgba32> image, int width, int height, bool crop, CollageSlot slot)
    {
        image.Mutate(c => c.AutoOrient());
        if (crop)
        {
            double scale = Math.Max((double)width / image.Width, (double)height / image.Height);
            int cw = Math.Clamp((int)Math.Round(width / scale), 1, image.Width), ch = Math.Clamp((int)Math.Round(height / scale), 1, image.Height);
            int x = Math.Clamp((int)Math.Round(slot.FocusX * image.Width - cw / 2d), 0, image.Width - cw), y = Math.Clamp((int)Math.Round(slot.FocusY * image.Height - ch / 2d), 0, image.Height - ch);
            image.Mutate(c => c.Crop(new Rectangle(x,y,cw,ch)).Resize(width,height));
        }
        else image.Mutate(c => c.Resize(new ResizeOptions { Size = new Size(width,height), Mode = ResizeMode.Max }));
    }
}

