using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Collage;

public interface ICollageColorConverter
{
    IccProfile? OutputProfile();
    Image<Rgba32> ConvertToSrgb(Image<Rgba32> image, string temporaryDirectory);
}

internal sealed class UnmanagedCollageColorConverter : ICollageColorConverter
{
    public IccProfile? OutputProfile() => null;
    public Image<Rgba32> ConvertToSrgb(Image<Rgba32> image, string temporaryDirectory)
    {
        image.Mutate(c => c.AutoOrient());
        if (image.Metadata.IccProfile is not null)
            throw new InvalidDataException("该平台尚未配置 ICC 色彩转换，已停止导出。");
        return image.Clone();
    }
}
