using System.IO;
using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Services;

public sealed class MapThumbnailCache
{
    public MapThumbnailCache(string? directory = null) =>
        Directory = directory ?? Path.Combine(AppDataPaths.Root, "map-thumbnails");

    public string Directory { get; }

    public async Task<string?> GetUrlAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath)) return null;
        var info = new FileInfo(sourcePath);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{Path.GetFullPath(sourcePath)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}")))[..24].ToLowerInvariant();
        System.IO.Directory.CreateDirectory(Directory);
        var output = Path.Combine(Directory, key + ".jpg");
        if (!File.Exists(output))
        {
            try
            {
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(sourcePath, cancellationToken).ConfigureAwait(false);
                image.Mutate(context => context.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(220, 160), Mode = ResizeMode.Crop, Position = AnchorPositionMode.Center
                }));
                await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 82 }, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch { return null; }
        }
        return $"https://hanabe-thumbs.local/{Path.GetFileName(output)}";
    }
}
