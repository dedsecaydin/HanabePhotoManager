using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;

namespace HanabePhotoManager.App.Services;

/// <summary>串行解码、原子发布且有磁盘容量上限的地图缩略图缓存。</summary>
public sealed class MapThumbnailCache
{
    // Shared across instances: duplicate requests cannot decode/write the same cache entry together.
    private static readonly SemaphoreSlim DecodeGate = new(1, 1);
    private readonly long _maximumBytes;
    private long _lastTrimTick;
    public MapThumbnailCache(string? directory = null, long maximumBytes = 256L * 1024 * 1024)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        Directory = Path.GetFullPath(directory ?? Path.Combine(AppDataPaths.Root, "map-thumbnails"));
        _maximumBytes = maximumBytes;
    }

    public string Directory { get; }

    public async Task<string?> GetUrlAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await DecodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Both metadata IO and CPU image transforms stay off the UI thread, including cache hits.
            return await Task.Run(() => GetCoreAsync(sourcePath, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally { DecodeGate.Release(); }
    }

    private async Task<string?> GetCoreAsync(string sourcePath, CancellationToken token)
    {
        string? temporary = null;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(sourcePath)) return null;
            var info = new FileInfo(sourcePath);
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}")))[..24].ToLowerInvariant();
            System.IO.Directory.CreateDirectory(Directory);
            if ((File.GetAttributes(Directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("缩略图缓存目录不能是重定向链接。");
            var output = Path.Combine(Directory, key + ".jpg");
            if (File.Exists(output) && (File.GetAttributes(output) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("缩略图缓存文件不能是重定向链接。");
            if (!File.Exists(output) || new FileInfo(output).Length == 0)
            {
                var dimensions = await Image.IdentifyAsync(sourcePath, token).ConfigureAwait(false);
                if ((long)dimensions.Width * dimensions.Height > 100_000_000)
                {
                    Trace.TraceWarning("地图缩略图超过单张解码像素预算。");
                    return null;
                }
                using var image = await Image.LoadAsync<Rgba32>(new DecoderOptions
                {
                    MaxFrames = 1,
                    TargetSize = new Size(440, 320)
                }, sourcePath, token).ConfigureAwait(false);
                image.Mutate(context => context.AutoOrient().Resize(new ResizeOptions
                {
                    Size = new Size(220, 160), Mode = ResizeMode.Crop, Position = AnchorPositionMode.Center
                }));
                temporary = Path.Combine(Directory, key + "." + Guid.NewGuid().ToString("N") + ".tmp");
                await image.SaveAsJpegAsync(temporary, new JpegEncoder { Quality = 82 }, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, output, overwrite: true);
            }
            File.SetLastWriteTimeUtc(output, DateTime.UtcNow);
            // A cache hit must not rescan the whole directory for every visible marker.
            // New entries always enforce the budget; hits periodically account for other writers.
            if (temporary is not null || _lastTrimTick == 0 || Environment.TickCount64 - _lastTrimTick >= 60_000)
            {
                TrimCache(token);
                _lastTrimTick = Environment.TickCount64;
            }
            return File.Exists(output) ? $"https://hanabe-thumbs.local/{Path.GetFileName(output)}" : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or UnknownImageFormatException or InvalidImageContentException)
        {
            Trace.TraceWarning("地图缩略图不可用：{0}", ex.Message);
            return null;
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void TrimCache(CancellationToken token)
    {
        // Never remove unrelated files, directories, or redirected entries in a custom cache root.
        var files = new DirectoryInfo(Directory).EnumerateFiles("*.jpg")
            .Where(file => file.Name.Length == 28 && file.Name[..24].All(Uri.IsHexDigit)
                && (file.Attributes & FileAttributes.ReparsePoint) == 0)
            .OrderBy(file => file.LastWriteTimeUtc).ToArray();
        var total = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            if (total <= _maximumBytes) break;
            var length = file.Length;
            file.Delete();
            total -= length;
        }
    }
}
