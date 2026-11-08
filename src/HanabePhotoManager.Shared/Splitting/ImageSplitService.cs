using Rectangle = SixLabors.ImageSharp.Rectangle;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace HanabePhotoManager.App.Splitting;

public sealed class ImageSplitService
{
    public static IReadOnlyList<Rectangle> Plan(Rectangle crop, int columns, int rows)
    {
        if (columns < 1 || rows < 1 || columns > 3 || rows > 3 || crop.X < 0 || crop.Y < 0 || crop.Width < columns || crop.Height < rows)
            throw new ArgumentException("选区太小或分割参数无效。请选择更大的区域。");
        var result = new List<Rectangle>();
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int left = crop.X + column * crop.Width / columns, top = crop.Y + row * crop.Height / rows;
                result.Add(new(left, top, (column + 1) * crop.Width / columns - column * crop.Width / columns,
                    (row + 1) * crop.Height / rows - row * crop.Height / rows));
            }
        return result;
    }

    public static async Task<Image> LoadAsync(string path, CancellationToken token)
    {
        var info = await Image.IdentifyAsync(path, token);
        if ((long)info.Width * info.Height > 60_000_000) throw new ArgumentException("图片超过 6000 万像素，请先缩小图片。");
        var image = await Image.LoadAsync(path, token);
        try { image.Mutate(x => x.AutoOrient()); return image; }
        catch { image.Dispose(); throw; }
    }

    public async Task<string> ExportAsync(string source, Rectangle crop, int columns, int rows, string output,
        bool png, IProgress<double>? progress, CancellationToken token)
    {
        output = Path.GetFullPath(output);
        if (!output.StartsWith(@"D:\", StringComparison.OrdinalIgnoreCase) || output.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("请选择 D 盘内的输出目录，不能保存到 OneDrive。");
        var tiles = Plan(crop, columns, rows);
        using var image = await LoadAsync(source, token);
        if (crop.Right > image.Width || crop.Bottom > image.Height) throw new ArgumentException("裁切区域超出图片边界，请重新选图。");
        Directory.CreateDirectory(output);
        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(output, ".pending-" + id);
        var destination = Path.Combine(output, "切图-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + id[..8]);
        Directory.CreateDirectory(staging);
        try
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                using var tile = image.Clone(x => x.Crop(tiles[i]));
                // A baked orientation must not be applied a second time by other viewers.
                tile.Metadata.ExifProfile = null;
                var path = Path.Combine(staging, $"{i + 1:00}." + (png ? "png" : "jpg"));
                if (png) await tile.SaveAsPngAsync(path, token);
                else await tile.SaveAsJpegAsync(path, new JpegEncoder { Quality = 95 }, token);
                progress?.Report((i + 1) * 100.0 / tiles.Count);
            }
            token.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            return destination;
        }
        catch { Directory.Delete(staging, true); throw; }
    }
}

