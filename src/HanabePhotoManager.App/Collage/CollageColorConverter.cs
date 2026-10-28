using System.IO;
using SixLabors.ImageSharp.Processing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SharpImage = SixLabors.ImageSharp.Image;
namespace HanabePhotoManager.App.Collage;
/// <summary>Windows Imaging Component converts each tagged source into the same sRGB destination.</summary>
internal static class CollageColorConverter
{
    internal static string SrgbProfilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "drivers", "color", "sRGB Color Space Profile.icm");
    internal static IccProfile? OutputProfile() => File.Exists(SrgbProfilePath) ? new IccProfile(File.ReadAllBytes(SrgbProfilePath)) : null;
    internal static Image<Rgba32> ConvertToSrgb(Image<Rgba32> image, string temporaryDirectory)
    {
        image.Mutate(c => c.AutoOrient());
        if (image.Metadata.IccProfile is null) return image.Clone();
        if (!File.Exists(SrgbProfilePath)) throw new InvalidDataException("系统 sRGB 配置缺失，无法验证 ICC 转换，已停止导出。");

        try
        {
            var profileBytes=image.Metadata.IccProfile.ToByteArray();
            if(profileBytes.Length>4*1024*1024)throw new InvalidDataException("ICC 配置超过 4 MB 预算。");
            // A tiny in-memory PNG carries the exact ICC profile into WIC without a locked temp file.
            using var carrier=new Image<Rgba32>(1,1);carrier.Metadata.IccProfile=image.Metadata.IccProfile.DeepClone();
            using var profileStream=new MemoryStream();carrier.SaveAsPng(profileStream);profileStream.Position=0;
            var decoder=BitmapDecoder.Create(profileStream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            var inputContext=decoder.Frames[0].ColorContexts?.FirstOrDefault()??throw new InvalidDataException("WIC 无法读取 ICC 配置。");            using var bgra = image.CloneAs<Bgra32>();
            var bytes = new byte[checked(image.Width * image.Height * 4)]; bgra.CopyPixelDataTo(bytes);
            var source = BitmapSource.Create(image.Width, image.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, bytes, image.Width * 4);
            source.Freeze();
            var converted = new ColorConvertedBitmap(source, inputContext, new ColorContext(new Uri(SrgbProfilePath)), System.Windows.Media.PixelFormats.Bgra32);
            converted.CopyPixels(bytes, image.Width * 4, 0);
            using var pixels = SharpImage.LoadPixelData<Bgra32>(bytes, image.Width, image.Height);
            return pixels.CloneAs<Rgba32>();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { throw new InvalidDataException("ICC 色彩转换失败，未发布拼图。" + ex.Message, ex); }

    }
}





