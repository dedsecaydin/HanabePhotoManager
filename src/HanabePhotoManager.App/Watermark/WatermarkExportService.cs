using System.Collections.Concurrent;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SharpImage = SixLabors.ImageSharp.Image;
using SharpPoint = SixLabors.ImageSharp.Point;

namespace HanabePhotoManager.App.Watermark;

public enum WatermarkMode { Signature, Tiled }
public enum WatermarkExportStatus { Success, Skipped, Failed }
public sealed record WatermarkExportOptions(string OutputDirectory, string Suffix, bool PreserveMetadata, WatermarkMode Mode,
    WatermarkLayoutSettings Signature, WatermarkTileSettings Tiled, int MaxParallelism = 0);
public sealed record WatermarkExportResult(string SourcePath, string? OutputPath, WatermarkExportStatus Status, string Message);
public sealed record WatermarkBatchProgress(int Completed, int Total, int Success, int Failed, string CurrentFile);

public sealed class WatermarkExportService
{
    public async Task<IReadOnlyList<WatermarkExportResult>> ExportAsync(IReadOnlyList<string> sources, string watermarkPath,
        WatermarkExportOptions options, IProgress<WatermarkBatchProgress>? progress = null, CancellationToken token = default)
    {
        if (!string.Equals(Path.GetExtension(watermarkPath), ".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("水印必须是 PNG 文件。");
        using var watermark = await SharpImage.LoadAsync<Rgba32>(watermarkPath, token).ConfigureAwait(false);
        if (!HasTransparency(watermark)) throw new InvalidDataException("PNG 水印必须包含透明像素。");
        Directory.CreateDirectory(options.OutputDirectory);
        var results = new ConcurrentBag<WatermarkExportResult>();
        var completed = 0; var success = 0; var failed = 0;
        var parallel = options.MaxParallelism > 0 ? options.MaxParallelism : Math.Clamp(Environment.ProcessorCount / 2, 1, 6);
        await Parallel.ForEachAsync(sources, new ParallelOptions { MaxDegreeOfParallelism = parallel, CancellationToken = token }, async (source, ct) =>
        {
            WatermarkExportResult result;
            try { result = await ExportOneAsync(source, watermark, options, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { result = new(source, null, WatermarkExportStatus.Failed, ex.Message); }
            results.Add(result);
            if (result.Status == WatermarkExportStatus.Success) Interlocked.Increment(ref success); else Interlocked.Increment(ref failed);
            var done = Interlocked.Increment(ref completed);
            progress?.Report(new(done, sources.Count, success, failed, Path.GetFileName(source)));
        }).ConfigureAwait(false);
        return results.OrderBy(x => x.SourcePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static async Task<WatermarkExportResult> ExportOneAsync(string source, Image<Rgba32> watermark, WatermarkExportOptions options, CancellationToken token)
    {
        using var image = await SharpImage.LoadAsync<Rgba32>(source, token).ConfigureAwait(false);
        if (!options.PreserveMetadata) { image.Metadata.ExifProfile = null; image.Metadata.IccProfile = null; image.Metadata.XmpProfile = null; }
        var placements = options.Mode == WatermarkMode.Signature
            ? new[] { WatermarkLayoutCalculator.CalculateSingle(image.Width, image.Height, watermark.Width, watermark.Height, options.Signature) }
            : WatermarkLayoutCalculator.CalculateTiled(image.Width, image.Height, watermark.Width, watermark.Height, options.Tiled);
        foreach (var p in placements)
        {
            token.ThrowIfCancellationRequested();
            using var mark = watermark.Clone(ctx => ctx.Resize(p.Width, p.Height).Rotate((float)p.RotationDegrees));
            var x = p.X - (mark.Width - p.Width) / 2; var y = p.Y - (mark.Height - p.Height) / 2;
            image.Mutate(ctx => ctx.DrawImage(mark, new SharpPoint(x, y), (float)p.Opacity));
        }
        var extension = Path.GetExtension(source).ToLowerInvariant();
        var encoder = Encoder(extension) ?? throw new NotSupportedException($"无法按原格式导出：{extension}");
        var output = UniquePath(options.OutputDirectory, Path.GetFileNameWithoutExtension(source) + (string.IsNullOrWhiteSpace(options.Suffix) ? "_watermarked" : options.Suffix), extension);
        var temp = output + $".{Guid.NewGuid():N}.tmp";
        try { await image.SaveAsync(temp, encoder, token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); File.Move(temp, output); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return new(source, output, WatermarkExportStatus.Success, "已导出");
    }

    private static bool HasTransparency(Image<Rgba32> image)
    {
        var transparent = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height && !transparent; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++) if (row[x].A < 255) { transparent = true; break; }
            }
        });
        return transparent;
    }

    private static IImageEncoder? Encoder(string ext) => ext switch { ".jpg" or ".jpeg" => new JpegEncoder { Quality = 95 }, ".png" => new PngEncoder(), ".webp" => new WebpEncoder { Quality = 95 }, ".bmp" => new BmpEncoder(), ".tif" or ".tiff" => new TiffEncoder(), _ => null };
    private static string UniquePath(string directory, string name, string ext)
    { var path = Path.Combine(directory, name + ext); for (var i = 1; File.Exists(path); i++) path = Path.Combine(directory, $"{name} ({i}){ext}"); return path; }
}
