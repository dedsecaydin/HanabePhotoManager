using System.IO;
using Image = SixLabors.ImageSharp.Image;
using Color = SixLabors.ImageSharp.Color;
using Size = SixLabors.ImageSharp.Size;
using Point = SixLabors.ImageSharp.Point;
using HanabePhotoManager.Core.Collage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Compression;
public enum CollageFileFormat { Jpeg, Png, Tiff }
public sealed record CollageExportOptions(string Directory, CollageFileFormat Format = CollageFileFormat.Jpeg, string Background = "#FFFFFF", bool Crop = false);
public sealed partial class ImageCollageService
{
    public static async Task<IReadOnlyList<CollagePhoto>> ProbeAsync(IReadOnlyList<string> paths, CancellationToken token)
    {
        var photos = new List<CollagePhoto>();
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            var info = await Image.IdentifyAsync(path, token).ConfigureAwait(false) ?? throw new InvalidDataException("无法识别图片。");
            if ((long)info.Width * info.Height > CollagePlanner.MaximumPixels) throw new InvalidDataException("单张图片超过 6000 万像素预算。");
            ushort orientation = 1; if (info.Metadata.ExifProfile?.TryGetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation, out var orientationValue) == true) orientation = orientationValue.Value;
            photos.Add(new(path, orientation >= 5 ? info.Height : info.Width, orientation >= 5 ? info.Width : info.Height));
        }
        return photos;
    }

    public async Task<CollageResult> ComposeAsync(CollagePlan plan, CollageExportOptions options, IProgress<CollageProgress>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var work = await HanabePhotoManager.App.Collage.CollageWorkGate.EnterAsync(token).ConfigureAwait(false);
        if (plan.Width < 1 || plan.Height < 1 || (long)plan.Width * plan.Height > CollagePlanner.MaximumPixels) throw new ArgumentException("画布超过像素预算。");
        if (plan.Slots.Count == 0 || plan.Slots.Select(s => s.Id).Distinct().Count() != plan.Slots.Count || plan.Slots.Any(s => s.X < 0 || s.Y < 0 || s.Width < 1 || s.Height < 1 || (long)s.X + s.Width > plan.Width || (long)s.Y + s.Height > plan.Height)) throw new ArgumentException("布局无效。");
        var photos = await ProbeAsync(plan.Slots.Select(s => s.Id).ToArray(), token).ConfigureAwait(false);
        // Conservative peak budget: canvas plus one decoded source and processing buffers.
        if (!CollagePlanner.FitsMemoryBudget((long)plan.Width * plan.Height, photos.Max(p => (long)p.Width * p.Height))) return new(false, null, 0, null, plan.Width, plan.Height, "预计内存超过 768 MB 预算，请减小画布或输入图片。");
        using var canvas = new Image<Rgba32>(plan.Width, plan.Height, Color.ParseHex(options.Background.TrimStart('#')));
        for (int i = 0; i < plan.Slots.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var slot = plan.Slots[i];
            using var sourceImage = await Image.LoadAsync<Rgba32>(new SixLabors.ImageSharp.Formats.DecoderOptions { MaxFrames = 1 }, slot.Id, token).ConfigureAwait(false);
            using var image = HanabePhotoManager.App.Collage.CollageColorConverter.ConvertToSrgb(sourceImage, Path.Combine(options.Directory, ".collage-tmp"));
            HanabePhotoManager.App.Collage.CollageImagePlacement.Resize(image, slot.Width, slot.Height, options.Crop, slot);
            canvas.Mutate(c => c.DrawImage(image, new Point(slot.X + (slot.Width - image.Width) / 2, slot.Y + (slot.Height - image.Height) / 2), 1));
            progress?.Report(new(i + 1, plan.Slots.Count, Path.GetFileName(slot.Id)));
        }
        canvas.Metadata.IccProfile = HanabePhotoManager.App.Collage.CollageColorConverter.OutputProfile();
        Directory.CreateDirectory(options.Directory);
        string extension = options.Format switch { CollageFileFormat.Png => ".png", CollageFileFormat.Tiff => ".tiff", _ => ".jpg" };
        var output = Path.Combine(options.Directory, $"拼图-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{extension}");
        string temp = output + ".tmp";
        try
        {
            IImageEncoder encoder = options.Format switch { CollageFileFormat.Png => new PngEncoder(), CollageFileFormat.Tiff => new TiffEncoder(), _ => new JpegEncoder { Quality = 95 } };
            await canvas.SaveAsync(temp, encoder, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temp, output, false);
            return new(true, output, new FileInfo(output).Length, options.Format == CollageFileFormat.Jpeg ? 95 : null, plan.Width, plan.Height, "导出完成：ICC 输入已通过 Windows WIC 转换为 sRGB；无配置输入按 sRGB 假定。");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}







