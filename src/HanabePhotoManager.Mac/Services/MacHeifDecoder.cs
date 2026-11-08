using System.Diagnostics;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace HanabePhotoManager.Mac.Services;

/// <summary>Decode via macOS sips into a private temporary PNG; the input is never modified.</summary>
public sealed class MacHeifDecoder
{
    private readonly string _temporaryRoot;
    private readonly Action<string, string, CancellationToken> _convert;

    public MacHeifDecoder(string temporaryRoot, Action<string, string, CancellationToken>? convert = null)
    {
        _temporaryRoot = temporaryRoot;
        _convert = convert ?? ConvertWithSips;
    }

    public Image<Rgb24> Decode(string source, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_temporaryRoot);
        var output = Path.Combine(_temporaryRoot, Guid.NewGuid().ToString("N") + ".png");
        try
        {
            _convert(source, output, token);
            token.ThrowIfCancellationRequested();
            return Image.Load<Rgb24>(output);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    private static void ConvertWithSips(string source, string output, CancellationToken token)
    {
        var info = new ProcessStartInfo("/usr/bin/sips")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "-s", "format", "png", source, "--out", output }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException("无法启动 HEIF 解码器。");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try { process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            process.WaitForExit();
            token.ThrowIfCancellationRequested();
            throw new IOException("HEIF 解码超时。");
        }
        _ = stdout.GetAwaiter().GetResult();
        var error = stderr.GetAwaiter().GetResult();
        if (process.ExitCode != 0 || !File.Exists(output)) throw new IOException($"HEIF 解码失败：{error}");
    }
}
