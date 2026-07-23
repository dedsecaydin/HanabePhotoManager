using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SharpImage = SixLabors.ImageSharp.Image;

namespace HanabePhotoManager.App.Compression;

public enum CompressionItemStatus
{
    Success,
    Unreachable,
    Skipped,
    Failed
}

public sealed record CompressionOptions(
    string OutputDirectory,
    bool PreserveMetadata = true,
    bool PreserveGps = true,
    int MinimumQuality = 20);

public sealed record CompressionItemResult(
    string SourcePath,
    string? OutputPath,
    CompressionItemStatus Status,
    long OriginalBytes,
    long OutputBytes,
    int? Quality,
    string Message);

public sealed class ImageCompressionService
{
    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".arw", ".cr2", ".cr3", ".nef", ".dng", ".raf", ".orf", ".rw2"
    };

    public async Task<CompressionItemResult> CompressAsync(
        CompressionWorkItem workItem,
        CompressionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(options);
        var sourcePath = Path.GetFullPath(workItem.Source.Path);
        var originalBytes = new FileInfo(sourcePath).Length;
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (RawExtensions.Contains(extension))
        {
            return Result(CompressionItemStatus.Skipped, null, 0, null, "RAW 原文件保持不变；未取得可压缩预览。 ");
        }

        try
        {
            Directory.CreateDirectory(options.OutputDirectory);
            using var image = await SharpImage.LoadAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            if (!options.PreserveMetadata) image.Metadata.ExifProfile = null;

            var encoded = extension switch
            {
                ".jpg" or ".jpeg" => await FindBestQualityAsync(image, workItem.TargetBytes, options.MinimumQuality,
                    quality => new JpegEncoder { Quality = quality }, cancellationToken).ConfigureAwait(false),
                ".webp" => await FindBestQualityAsync(image, workItem.TargetBytes, options.MinimumQuality,
                    quality => new WebpEncoder { Quality = quality }, cancellationToken).ConfigureAwait(false),
                ".png" => await EncodeOnceAsync(image, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression }, cancellationToken)
                    .ConfigureAwait(false),
                _ => await FindBestQualityAsync(image, workItem.TargetBytes, options.MinimumQuality,
                    quality => new JpegEncoder { Quality = quality }, cancellationToken).ConfigureAwait(false)
            };

            if (encoded.Bytes.Length > workItem.TargetBytes)
            {
                return Result(CompressionItemStatus.Unreachable, null, encoded.Bytes.Length, encoded.Quality,
                    "保持原分辨率时无法达到目标大小。 ");
            }

            var outputExtension = extension is ".jpg" or ".jpeg" or ".png" or ".webp" ? extension : ".jpg";
            var outputPath = UniqueOutputPath(options.OutputDirectory, Path.GetFileNameWithoutExtension(sourcePath), outputExtension);
            var temporaryPath = outputPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, encoded.Bytes, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, outputPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }

            return Result(CompressionItemStatus.Success, outputPath, encoded.Bytes.Length, encoded.Quality, "压缩完成");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result(CompressionItemStatus.Failed, null, 0, null, ex.Message);
        }

        CompressionItemResult Result(CompressionItemStatus status, string? outputPath, long outputBytes, int? quality, string message) =>
            new(sourcePath, outputPath, status, originalBytes, outputBytes, quality, message);
    }

    private static async Task<EncodedImage> FindBestQualityAsync(
        SharpImage image,
        long targetBytes,
        int minimumQuality,
        Func<int, IImageEncoder> encoderFactory,
        CancellationToken cancellationToken)
    {
        var low = Math.Clamp(minimumQuality, 1, 100);
        var high = 100;
        EncodedImage? best = null;
        EncodedImage? smallest = null;
        while (low <= high)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quality = low + (high - low) / 2;
            var candidate = await EncodeOnceAsync(image, encoderFactory(quality), cancellationToken, quality).ConfigureAwait(false);
            if (smallest is null || candidate.Bytes.Length < smallest.Bytes.Length) smallest = candidate;
            if (candidate.Bytes.Length <= targetBytes)
            {
                best = candidate;
                low = quality + 1;
            }
            else
            {
                high = quality - 1;
            }
        }

        return best ?? smallest ?? await EncodeOnceAsync(image, encoderFactory(minimumQuality), cancellationToken, minimumQuality)
            .ConfigureAwait(false);
    }

    private static async Task<EncodedImage> EncodeOnceAsync(
        SharpImage image,
        IImageEncoder encoder,
        CancellationToken cancellationToken,
        int? quality = null)
    {
        await using var stream = new MemoryStream();
        await image.SaveAsync(stream, encoder, cancellationToken).ConfigureAwait(false);
        return new EncodedImage(stream.ToArray(), quality);
    }

    private static string UniqueOutputPath(string directory, string name, string extension)
    {
        var candidate = Path.Combine(directory, name + extension);
        if (!File.Exists(candidate)) return candidate;
        for (var index = 1; ; index++)
        {
            candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private sealed record EncodedImage(byte[] Bytes, int? Quality);
}
