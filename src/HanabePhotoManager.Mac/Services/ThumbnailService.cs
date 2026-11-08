using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.Mac.Services;

/// <summary>
/// 缩略图：JPG / PNG / TIFF 等用 ImageSharp 缩小解码；
/// macOS 上的 RAW / HEIC 用系统自带的 sips 转出小 JPEG（Apple 的 RAW 解码，无需额外安装）。
/// 结果缓存在应用数据目录，最多 4 个并发，避免读卡时卡顿。
/// </summary>
public static class ThumbnailService
{
    private static readonly SemaphoreSlim Gate = new(4);
    private static readonly HashSet<string> ImageSharpExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" };
    private static readonly HashSet<string> SipsExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".heic", ".heif", ".arw", ".cr2", ".cr3", ".nef", ".dng", ".raf", ".orf", ".rw2", ".srw", ".pef" };

    private static string CacheDirectory
    {
        get
        {
            var dir = Path.Combine(MacSettingsStore.DataDirectory, "thumbnails");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static bool CanRender(string path)
    {
        var ext = Path.GetExtension(path);
        return ImageSharpExtensions.Contains(ext) || (OperatingSystem.IsMacOS() && SipsExtensions.Contains(ext));
    }

    public static async Task<Bitmap?> LoadAsync(string path, int size = 240, CancellationToken token = default)
    {
        if (!CanRender(path)) return null;
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Render(path, size), token).ConfigureAwait(false);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static Bitmap? Render(string path, int size)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return null;
        var key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{size}")));
        var cached = Path.Combine(CacheDirectory, key + ".jpg");
        if (!File.Exists(cached))
        {
            if (ImageSharpExtensions.Contains(info.Extension))
            {
                using var image = Image.Load(new DecoderOptions { TargetSize = new SixLabors.ImageSharp.Size(size * 2, size * 2) }, path);
                image.Mutate(context => context.AutoOrient().Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new SixLabors.ImageSharp.Size(size, size) }));
                image.Save(cached, new JpegEncoder { Quality = 82 });
            }
            else if (!RunSips(path, cached, size))
            {
                return null;
            }
        }

        using var stream = File.OpenRead(cached);
        return new Bitmap(stream);
    }

    /// <summary>macOS：sips -s format jpeg -Z 尺寸 原图 --out 缩略图。</summary>
    private static bool RunSips(string source, string destination, int size)
    {
        using var process = Process.Start(new ProcessStartInfo("/usr/bin/sips")
        {
            ArgumentList = { "-s", "format", "jpeg", "-Z", size.ToString(), source, "--out", destination },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        if (process is null) return false;
        if (!process.WaitForExit(15000)) { try { process.Kill(); } catch (InvalidOperationException) { } return false; }
        return process.ExitCode == 0 && File.Exists(destination);
    }
}
