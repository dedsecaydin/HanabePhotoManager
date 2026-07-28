using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SharpColor = SixLabors.ImageSharp.Color;
using SharpImage = SixLabors.ImageSharp.Image;
using SharpPoint = SixLabors.ImageSharp.Point;

namespace HanabePhotoManager.App.Compression;

public enum CollageOrientation
{
    Vertical,
    Horizontal
}

public sealed record CollageOptions(
    string OutputDirectory,
    CollageOrientation Orientation,
    long? TargetBytes,
    int MinimumQuality = 20);

public sealed record CollageProgress(int Processed, int Total, string CurrentFile);

public sealed record CollageResult(
    bool IsSuccess,
    string? OutputPath,
    long OutputBytes,
    int? Quality,
    int Width,
    int Height,
    string Message);

public sealed class ImageCollageService
{
    public async Task<CollageResult> ComposeAsync(
        IReadOnlyList<string> sourcePaths,
        CollageOptions options,
        IProgress<CollageProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(options);
        if (sourcePaths.Count == 0)
            return new(false, null, 0, null, 0, 0, "请先添加至少一张图片");

        cancellationToken.ThrowIfCancellationRequested();
        var images = new List<SixLabors.ImageSharp.Image<Rgba32>>(sourcePaths.Count);
        try
        {
            for (var index = 0; index < sourcePaths.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.GetFullPath(sourcePaths[index]);
                var image = await SharpImage.LoadAsync<Rgba32>(path, cancellationToken).ConfigureAwait(false);
                image.Mutate(context => context.AutoOrient());
                images.Add(image);
                progress?.Report(new(index + 1, sourcePaths.Count, Path.GetFileName(path)));
            }

            var width64 = options.Orientation == CollageOrientation.Vertical
                ? images.Max(image => (long)image.Width)
                : images.Sum(image => (long)image.Width);
            var height64 = options.Orientation == CollageOrientation.Vertical
                ? images.Sum(image => (long)image.Height)
                : images.Max(image => (long)image.Height);
            if (width64 > int.MaxValue || height64 > int.MaxValue || width64 * height64 > int.MaxValue)
                return new(false, null, 0, null, 0, 0, "拼图像素尺寸过大，无法在本机安全分配画布");

            var width = (int)width64;
            var height = (int)height64;
            using var canvas = new SixLabors.ImageSharp.Image<Rgba32>(width, height, SharpColor.White);
            var offset = 0;
            foreach (var image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var point = options.Orientation == CollageOrientation.Vertical
                    ? new SharpPoint((width - image.Width) / 2, offset)
                    : new SharpPoint(offset, (height - image.Height) / 2);
                canvas.Mutate(context => context.DrawImage(image, point, 1f));
                offset += options.Orientation == CollageOrientation.Vertical ? image.Height : image.Width;
            }

            var encoded = await EncodeAsync(canvas, options.TargetBytes, options.MinimumQuality, cancellationToken)
                .ConfigureAwait(false);
            if (options.TargetBytes is { } target && encoded.Bytes.LongLength > target)
                return new(false, null, encoded.Bytes.LongLength, encoded.Quality, width, height,
                    "保持原始像素尺寸时无法达到指定大小");

            Directory.CreateDirectory(options.OutputDirectory);
            var outputPath = UniqueOutputPath(options.OutputDirectory);
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

            return new(true, outputPath, encoded.Bytes.LongLength, encoded.Quality, width, height,
                $"拼图完成：{width:N0} × {height:N0}");
        }
        finally
        {
            foreach (var image in images) image.Dispose();
        }
    }

    private static async Task<(byte[] Bytes, int Quality)> EncodeAsync(
        SixLabors.ImageSharp.Image<Rgba32> image,
        long? targetBytes,
        int minimumQuality,
        CancellationToken cancellationToken)
    {
        if (targetBytes is null)
            return (await EncodeOnceAsync(image, 95, cancellationToken).ConfigureAwait(false), 95);

        var low = Math.Clamp(minimumQuality, 1, 100);
        var high = 100;
        byte[]? best = null;
        var bestQuality = low;
        byte[]? smallest = null;
        while (low <= high)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quality = low + (high - low) / 2;
            var bytes = await EncodeOnceAsync(image, quality, cancellationToken).ConfigureAwait(false);
            if (smallest is null || bytes.Length < smallest.Length) smallest = bytes;
            if (bytes.LongLength <= targetBytes.Value)
            {
                best = bytes;
                bestQuality = quality;
                low = quality + 1;
            }
            else
            {
                high = quality - 1;
            }
        }
        return (best ?? smallest ?? await EncodeOnceAsync(image, minimumQuality, cancellationToken).ConfigureAwait(false),
            best is null ? minimumQuality : bestQuality);
    }

    private static async Task<byte[]> EncodeOnceAsync(
        SixLabors.ImageSharp.Image<Rgba32> image,
        int quality,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await image.SaveAsJpegAsync(stream, new JpegEncoder { Quality = quality }, cancellationToken)
            .ConfigureAwait(false);
        return stream.ToArray();
    }

    private static string UniqueOutputPath(string directory)
    {
        var baseName = $"拼图-{DateTime.Now:yyyyMMdd-HHmmss}";
        var candidate = Path.Combine(directory, baseName + ".jpg");
        if (!File.Exists(candidate)) return candidate;
        for (var index = 1; ; index++)
        {
            candidate = Path.Combine(directory, $"{baseName} ({index}).jpg");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
