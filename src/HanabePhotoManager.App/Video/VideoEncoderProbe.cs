using System.Diagnostics;
using System.IO;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.Video;

internal sealed record EncoderCapability(VideoEncoder Encoder, int BitDepth, bool Available, string Reason);

internal static class VideoEncoderProbe
{
    internal static async Task<EncoderCapability> ProbeAsync(VideoEncoder encoder, int bitDepth, CancellationToken token)
    {
        var profile = new VideoProcessingProfile(SonySourceGamut.SGamut3Cine, null, encoder, bitDepth);
        try { profile.Validate(); }
        catch (Exception ex) { return new(encoder, bitDepth, false, ex.Message); }
        var ffmpeg = FfmpegExecutableLocator.Resolve();
        var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");
        if (!File.Exists(ffprobe)) return new(encoder, bitDepth, false, "缺少 ffprobe，无法验证实际输出位深。");
        var directory = Path.Combine(AppDataPaths.ProcessingRoot, "video", "probe");
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, $"encoder-{Guid.NewGuid():N}.mp4");
        await SLog3Transcoder.EncoderGate.WaitAsync(token);
        try
        {
            var info = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true };
            foreach (var arg in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-n", "-f", "lavfi", "-i",
                         "color=c=gray:s=320x240:r=30:d=1", "-frames:v", "1", "-pix_fmt", profile.PixelFormat,
                         "-c:v", profile.EncoderName, output }) info.ArgumentList.Add(arg);
            var (exitCode, error) = await RunAsync(info, token);
            if (exitCode != 0) return new(encoder, bitDepth, false, error);
            var probe = new ProcessStartInfo(ffprobe) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=pix_fmt",
                         "-of", "default=noprint_wrappers=1:nokey=1", output }) probe.ArgumentList.Add(arg);
            using var process = Process.Start(probe) ?? throw new IOException("ffprobe 无法启动。");
            using var cancel = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
            var actualTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(token); }
            catch (OperationCanceledException) { await process.WaitForExitAsync(); throw; }
            var actual = (await actualTask).Trim();
            var available = process.ExitCode == 0 && actual == profile.PixelFormat;
            return new(encoder, bitDepth, available, available ? "已通过实际一帧编码及输出位深检查" :
                $"实际像素格式 {actual}，预期 {profile.PixelFormat}。{await errorTask}");
        }
        finally
        {
            try { if (File.Exists(output)) File.Delete(output); } catch (IOException) { }
            SLog3Transcoder.EncoderGate.Release();
        }
    }

    private static async Task<(int ExitCode, string Error)> RunAsync(ProcessStartInfo info, CancellationToken token)
    {
        using var process = Process.Start(info) ?? throw new IOException("编码器探测无法启动 FFmpeg。");
        using var cancel = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var errorTask = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException) { await process.WaitForExitAsync(); throw; }
        return (process.ExitCode, (await errorTask).Trim());
    }
}
