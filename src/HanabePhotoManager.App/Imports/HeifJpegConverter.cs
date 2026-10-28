using System.IO;
using System.Text.Json;
using HanabePhotoManager.Infrastructure.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace HanabePhotoManager.App.Imports;

/// <summary>保留已验证的 HEIF 原件，原子生成 JPG，并以内容凭据实现重试幂等。</summary>
internal sealed class HeifJpegConverter
{
    internal static bool IsHeif(string path) => Path.GetExtension(path).ToUpperInvariant() is ".HEIF" or ".HEIC" or ".HIF";

    internal async Task<string> EnsureJpegAsync(string original, CancellationToken token, IReadOnlySet<string>? reservedPaths = null)
    {
        var hasher = new Sha256FileHasher();
        var sourceHash = await hasher.ComputeSha256Async(original, token).ConfigureAwait(false);
        var directory = Path.GetDirectoryName(original)!;
        var receiptPath = Path.Combine(directory, "." + Path.GetFileName(original) + ".hanabe-jpeg.json");
        Receipt? receipt = null;
        if (File.Exists(receiptPath))
        {
            try { receipt = JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(receiptPath, token).ConfigureAwait(false)); }
            catch (JsonException) { }
        }
        var safeReceipt = receipt is not null && receipt.SourceSha256 == sourceHash &&
            !string.IsNullOrWhiteSpace(receipt.JpegName) && Path.GetFileName(receipt.JpegName) == receipt.JpegName &&
            Path.GetExtension(receipt.JpegName).Equals(".jpg", StringComparison.OrdinalIgnoreCase);
        if (safeReceipt)
        {
            var existing = Path.Combine(directory, receipt!.JpegName);
            if (File.Exists(existing) && await hasher.ComputeSha256Async(existing, token).ConfigureAwait(false) == receipt.JpegSha256)
                return existing;
        }

        var output = safeReceipt && !File.Exists(Path.Combine(directory, receipt!.JpegName))
            ? Path.Combine(directory, receipt.JpegName) : AvailableName(original, reservedPaths);
        var partial = output + "." + Guid.NewGuid().ToString("N") + ".hanabe-jpeg-part";
        var receiptPartial = receiptPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var image = await Task.Run(() => HeifImageDecoder.Decode(original, token), token).ConfigureAwait(false);
            await image.SaveAsJpegAsync(partial, new JpegEncoder { Quality = 95 }, token).ConfigureAwait(false);
            using (var verified = await SixLabors.ImageSharp.Image.LoadAsync(partial, token).ConfigureAwait(false))
                if (verified.Width != image.Width || verified.Height != image.Height) throw new IOException("JPG 回读尺寸不符。");
            var jpegHash = await hasher.ComputeSha256Async(partial, token).ConfigureAwait(false);
            // 转换期间原件变化时不发布衍生文件。
            if (sourceHash != await hasher.ComputeSha256Async(original, token).ConfigureAwait(false))
                throw new IOException("HEIF 原件在转换期间发生变化，已保留待重试。");
            await File.WriteAllTextAsync(receiptPartial,
                JsonSerializer.Serialize(new Receipt(sourceHash, Path.GetFileName(output), jpegHash)), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            // 先保存已选名称和期望哈希；中断重试可识别已发布输出，不重复生成。
            File.Move(receiptPartial, receiptPath, overwrite: true);
            File.Move(partial, output, overwrite: false);
            File.SetLastWriteTimeUtc(output, File.GetLastWriteTimeUtc(original));
            return output;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
            if (File.Exists(receiptPartial)) File.Delete(receiptPartial);
        }
    }

    private static string AvailableName(string original, IReadOnlySet<string>? reservedPaths)
    {
        var path = Path.ChangeExtension(original, ".JPG");
        var directory = Path.GetDirectoryName(original)!;
        var stem = Path.GetFileNameWithoutExtension(original) + "_HEIF";
        for (var number = 1; File.Exists(path) || Directory.Exists(path) || reservedPaths?.Contains(path) == true; number++)
            path = Path.Combine(directory, stem + (number == 1 ? "" : "_" + number) + ".JPG");
        return path;
    }

    internal sealed record Receipt(string SourceSha256, string JpegName, string JpegSha256);
}
