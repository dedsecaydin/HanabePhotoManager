using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.Video;

internal static class SLog3Transcoder
{
    internal static readonly SemaphoreSlim EncoderGate = new(1, 1);
    internal static string WorkRoot => Path.Combine(AppDataPaths.ProcessingRoot, "video", "jobs");

    public static Task ExportAsync(string source, string outputDirectory, string lutPath, CancellationToken token) =>
        ExportAsync(source, outputDirectory, new VideoProcessingProfile(SonySourceGamut.SGamut3Cine, lutPath,
            VideoEncoder.H264Software, 8, VideoQualityMode.ConstantQuality, 18), token);

    public static async Task<string> ExportAsync(string source, string outputDirectory, VideoProcessingProfile profile,
        CancellationToken token, IProgress<double>? progress = null, IProgress<string>? events = null)
    {
        profile.Validate();
        if (!Path.IsPathFullyQualified(outputDirectory)) throw new ArgumentException("导出目录必须是绝对路径。");
        Directory.CreateDirectory(outputDirectory);
        var partial = Path.Combine(outputDirectory, $".slog3-{Guid.NewGuid():N}.mp4");
        var ownsPartial = false;
        try
        {
            await ConvertAsync(source, partial, profile, preview: false, token, progress, events);
            ownsPartial = true;
            token.ThrowIfCancellationRequested();
            // File.Move without overwrite is the final atomic reservation.
            for (var attempt = 0; ; attempt++)
            {
                var output = FindAvailableOutputPath(source, outputDirectory);
                try { File.Move(partial, output); return output; }
                catch (IOException) when (File.Exists(output) && attempt < 1000) { }
            }
        }
        finally { if (ownsPartial) TryDelete(partial); }
    }

    internal static string FindAvailableOutputPath(string source, string outputDirectory)
    {
        var stem = Path.GetFileNameWithoutExtension(source) + "_SLog3_Rec709";
        var candidate = Path.Combine(outputDirectory, stem + ".mp4");
        for (var index = 2; File.Exists(candidate); index++) candidate = Path.Combine(outputDirectory, $"{stem}_{index}.mp4");
        return candidate;
    }

    public static Task ConvertAsync(string source, string output, string lutPath, bool preview, CancellationToken token) =>
        ConvertAsync(source, output, new VideoProcessingProfile(SonySourceGamut.SGamut3Cine, lutPath,
            VideoEncoder.H264Software, 8, VideoQualityMode.ConstantQuality, preview ? 22 : 18), preview, token);

    public static async Task ConvertAsync(string source, string output, VideoProcessingProfile profile, bool preview,
        CancellationToken token, IProgress<double>? progress = null, IProgress<string>? events = null)
    {
        profile.Validate();
        if (!File.Exists(source)) throw new FileNotFoundException("源视频不存在。", source);
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            throw new IOException("导出位置不能覆盖原视频。");
        if (File.Exists(output)) throw new IOException("目标文件已存在，不能覆盖。");
        var staged = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, $".slog3-stage-{Guid.NewGuid():N}.mp4");
        var effective = profile;
        if (profile.Encoder is not (VideoEncoder.H264Software or VideoEncoder.H265Software))
        {
            var capability = await VideoEncoderProbe.ProbeAsync(profile.Encoder, profile.OutputBitDepth, token);
            if (!capability.Available)
            {
                if (!profile.AllowSoftwareFallback) throw new IOException($"硬件编码不可用：{capability.Reason}");
                effective = profile.SoftwareVersion();
                    events?.Report($"硬件探测失败，使用 {effective.EncoderName} 软件编码：{capability.Reason}");
            }
        }
        await EncoderGate.WaitAsync(token);
        var jobDirectory = Path.Combine(WorkRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(jobDirectory);
            if (profile.CustomLutPath is { Length: > 0 }) File.Copy(profile.CustomLutPath, Path.Combine(jobDirectory, "look.cube"));
            else SonyRec709Transform.WriteCube(Path.Combine(jobDirectory, "look.cube"), profile.SourceGamut);
            var ffmpeg = FfmpegExecutableLocator.Resolve();
            try { await RunFfmpegAsync(ffmpeg, source, staged, jobDirectory, effective, preview, token, progress); }
            catch (IOException ex) when (profile.AllowSoftwareFallback && effective.Encoder is not (VideoEncoder.H264Software or VideoEncoder.H265Software))
            {
                TryDelete(staged);
                events?.Report($"硬件编码运行失败，重试 {profile.SoftwareVersion().EncoderName} 软件编码：{ex.Message}");
                await RunFfmpegAsync(ffmpeg, source, staged, jobDirectory, profile.SoftwareVersion(), preview, token, progress);
            }
            token.ThrowIfCancellationRequested();
            if (!File.Exists(staged) || new FileInfo(staged).Length == 0) throw new IOException("FFmpeg 未生成有效视频。");
            await VerifyOutputAsync(ffmpeg, staged, profile, token);
            token.ThrowIfCancellationRequested();
            File.Move(staged, output); // atomic no-overwrite publication
        }
        finally
        {
            TryDelete(staged);
            EncoderGate.Release();
            try { if (Directory.Exists(jobDirectory)) Directory.Delete(jobDirectory, true); } catch (IOException) { }
        }
    }

    internal static string[] BuildArguments(string source, string output, VideoProcessingProfile profile, bool preview)
    {
        var args = new List<string> { "-nostdin", "-hide_banner", "-loglevel", "error", "-n", "-i", source };
        if (preview) { args.Add("-t"); args.Add("10"); }
        // Sony's reference S-Log3 code values are full-range. The input range and
        // YCbCr matrix are explicit because real files may be tagged differently.
        // The LUT works in float RGB. Encode the resulting SDR RGB with BT.709
        // coefficients into limited-range YCbCr, independently of metadata tags.
        args.AddRange(["-map", "0:v:0", "-map", "0:a?", "-vf",
            $"scale=in_range={(profile.InputRange == InputVideoRange.Full ? "full" : "limited")}:in_color_matrix={(profile.InputMatrix == InputYuvMatrix.Bt709 ? "bt709" : "bt2020")},format=gbrpf32le,lut3d=file=look.cube,scale=out_range=limited:out_color_matrix=bt709,format={profile.PixelFormat}"]);
        args.AddRange(profile.EncoderArguments(preview));
        if (profile.Encoder == VideoEncoder.H265Software)
            args.AddRange(["-x265-params", "colorprim=bt709:transfer=bt709:colormatrix=bt709"]);
        if (profile.Encoder == VideoEncoder.H264Software)
            args.AddRange(["-x264-params", "colorprim=bt709:transfer=bt709:colormatrix=bt709"]);
        args.AddRange(["-c:a", "aac", "-b:a", "192k", "-fps_mode", "passthrough", "-movflags", "+faststart",
            "-color_range", "tv",
            "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709", output]);
        return args.ToArray();
    }

    private static async Task RunFfmpegAsync(string ffmpeg, string source, string output, string directory,
        VideoProcessingProfile profile, bool preview, CancellationToken token, IProgress<double>? progress)
    {
        double? duration = null;
        if (progress is not null)
        {
            try { duration = await VideoSourceProbe.DurationSecondsAsync(source, token); }
            catch (IOException) { } // Time reporting is optional; encoding still proceeds.
            if (preview && duration is not null) duration = Math.Min(duration.Value, 10);
        }
        var info = new ProcessStartInfo(ffmpeg) { WorkingDirectory = directory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardError = true };
        if (progress is not null)
            foreach (var arg in new[] { "-stats_period", "0.5", "-progress", "pipe:2" }) info.ArgumentList.Add(arg);
        foreach (var arg in BuildArguments(source, output, profile, preview)) info.ArgumentList.Add(arg);
        using var process = Start(info);
        using var cancel = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var errors = ReadProgressAndErrorsAsync(process.StandardError, duration, progress);
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException) { await process.WaitForExitAsync(); await errors; throw; }
        var error = await errors;
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new IOException($"FFmpeg 转换失败：{error.Trim()}");
        progress?.Report(100);
    }

    private static async Task<string> ReadProgressAndErrorsAsync(StreamReader reader, double? duration, IProgress<double>? progress)
    {
        var errors = new StringBuilder();
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (duration is > 0 && (line.StartsWith("out_time_us=", StringComparison.Ordinal) ||
                                    line.StartsWith("out_time_ms=", StringComparison.Ordinal)))
            {
                var value = line[(line.IndexOf('=') + 1)..];
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
                    progress?.Report(Math.Clamp(100.0 * microseconds / (duration.Value * 1_000_000), 0, 99));
            }
            else if (!line.Contains('=') && errors.Length < 8192) errors.AppendLine(line);
        }
        return errors.ToString();
    }

    private static async Task VerifyOutputAsync(string ffmpeg, string output, VideoProcessingProfile profile, CancellationToken token)
    {
        var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");
        if (!File.Exists(ffprobe)) return;
        var info = new ProcessStartInfo(ffprobe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=pix_fmt,color_range,color_primaries,color_transfer,color_space", "-of", "default=noprint_wrappers=1", output }) info.ArgumentList.Add(arg);
        using var process = Start(info);
        using var cancel = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var dataTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException) { await process.WaitForExitAsync(); throw; }
        var data = await dataTask;
        if (process.ExitCode != 0 || !data.Contains($"pix_fmt={profile.PixelFormat}", StringComparison.Ordinal) ||
            !data.Contains("color_primaries=bt709", StringComparison.Ordinal) ||
            !data.Contains("color_transfer=bt709", StringComparison.Ordinal) ||
            !data.Contains("color_space=bt709", StringComparison.Ordinal) ||
            !data.Contains("color_range=tv", StringComparison.Ordinal))
            throw new IOException($"导出位深或 Rec.709 标签验证失败：{data} {await errorTask}");
    }

    private static Process Start(ProcessStartInfo info)
    {
        try { return Process.Start(info) ?? throw new IOException("FFmpeg 无法启动。"); }
        catch (Win32Exception ex) { throw new IOException($"视频工具无法启动：{ex.Message}", ex); }
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } }
}
