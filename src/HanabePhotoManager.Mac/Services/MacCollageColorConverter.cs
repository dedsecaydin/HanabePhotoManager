using System.Diagnostics;
using System.IO;
using HanabePhotoManager.App.Collage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.Mac.Services;

/// <summary>Converts tagged collage images with macOS ColorSync via sips.</summary>
internal sealed class MacCollageColorConverter(string profilePath, Action<string, string, string>? convert = null) : ICollageColorConverter
{
    private readonly Action<string, string, string> _convert = convert ?? ConvertWithSips;

    public IccProfile? OutputProfile() => File.Exists(profilePath) ? new IccProfile(File.ReadAllBytes(profilePath)) : null;

    public Image<Rgba32> ConvertToSrgb(Image<Rgba32> image, string temporaryDirectory)
    {
        image.Mutate(operation => operation.AutoOrient());
        if (image.Metadata.IccProfile is null) return image.Clone();
        if (!File.Exists(profilePath)) throw new InvalidDataException("系统 sRGB 配置缺失，无法验证 ICC 转换，已停止导出。");
        if (image.Metadata.IccProfile.ToByteArray().Length > 4 * 1024 * 1024) throw new InvalidDataException("ICC 配置超过 4 MB 预算。");
        Directory.CreateDirectory(temporaryDirectory);
        var name = Guid.NewGuid().ToString("N");
        var source = Path.Combine(temporaryDirectory, name + "-source.png");
        var output = Path.Combine(temporaryDirectory, name + "-converted.png");
        try
        {
            image.SaveAsPng(source);
            _convert(source, output, profilePath);
            var converted = Image.Load<Rgba32>(output);
            if (converted.Width != image.Width || converted.Height != image.Height || converted.Metadata.IccProfile is null ||
                !converted.Metadata.IccProfile.ToByteArray().AsSpan().SequenceEqual(File.ReadAllBytes(profilePath)))
            {
                converted.Dispose();
                throw new InvalidDataException("ICC 转换结果未通过尺寸或色彩配置校验。");
            }
            return converted;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not InvalidDataException)
        { throw new InvalidDataException("ICC 色彩转换失败，未发布拼图。", ex); }
        finally
        {
            if (File.Exists(source)) File.Delete(source);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    private static void ConvertWithSips(string source, string output, string profile)
    {
        var info = new ProcessStartInfo("/usr/bin/sips") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "--matchTo", profile, source, "--out", output }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException("无法启动色彩转换工具。");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000))
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            process.WaitForExit();
            throw new IOException("ICC 色彩转换超时。");
        }
        _ = stdout.GetAwaiter().GetResult();
        if (process.ExitCode != 0 || !File.Exists(output)) throw new IOException("ICC 色彩转换失败：" + stderr.GetAwaiter().GetResult());
    }
}
