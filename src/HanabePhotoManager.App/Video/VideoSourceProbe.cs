using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace HanabePhotoManager.App.Video;

internal sealed record VideoSourceInfo(int Width, int Height, string FrameRate, string Range,
    string Matrix, string Primaries, string Transfer)
{
    public string Summary => $"{Width}×{Height} · {FrameRate} fps · range={Range} · matrix={Matrix} · transfer={Transfer} · primaries={Primaries}";
}

internal static class VideoSourceProbe
{
    internal static async Task<double?> DurationSecondsAsync(string source, CancellationToken token)
    {
        var ffprobe = Path.Combine(Path.GetDirectoryName(FfmpegExecutableLocator.Resolve())!, "ffprobe.exe");
        if (!File.Exists(ffprobe)) return null;
        var info = new ProcessStartInfo(ffprobe) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", source }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("ffprobe 无法启动。");
        using var cancel = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException) { await process.WaitForExitAsync(); throw; }
        if (process.ExitCode != 0) throw new IOException($"无法读取视频时长：{await errorTask}");
        return double.TryParse((await outputTask).Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;
    }

    internal static async Task<VideoSourceInfo?> ReadAsync(string source, CancellationToken token)
    {
        var ffprobe = Path.Combine(Path.GetDirectoryName(FfmpegExecutableLocator.Resolve())!, "ffprobe.exe");
        if (!File.Exists(ffprobe)) return null;
        var info = new ProcessStartInfo(ffprobe) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries",
                     "stream=width,height,avg_frame_rate,color_range,color_space,color_primaries,color_transfer",
                     "-of", "json", source }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("ffprobe 无法启动。");
        using var cancel = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var dataTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException) { await process.WaitForExitAsync(); throw; }
        if (process.ExitCode != 0) throw new IOException($"无法读取视频参数：{await errorTask}");
        using var data = JsonDocument.Parse(await dataTask);
        var streams = data.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() == 0) return null;
        var stream = streams[0];
        string read(string name) => stream.TryGetProperty(name, out var value) ? value.ToString() : "unknown";
        int readInt(string name) => stream.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
        return new(readInt("width"), readInt("height"), read("avg_frame_rate"), read("color_range"),
            read("color_space"), read("color_primaries"), read("color_transfer"));
    }
}
