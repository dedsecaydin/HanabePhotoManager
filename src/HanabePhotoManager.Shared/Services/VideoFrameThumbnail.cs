using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HanabePhotoManager.App.Video;

namespace HanabePhotoManager.App.Services;

/// <summary>
/// 用 FFmpeg 抽取视频第一帧作为缩略图，结果缓存在应用数据目录 video-frames（按路径+大小+修改时间命名）。
/// 找不到 FFmpeg 或抽帧失败时返回 null，调用方继续使用系统缩略图。
/// </summary>
internal static class VideoFrameThumbnail
{
    private static readonly SemaphoreSlim Gate = new(2);

    public static string? TryGetFirstFrameJpeg(string videoPath)
    {
        try
        {
            var info = new FileInfo(videoPath);
            if (!info.Exists) return null;
            var key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(
                $"{info.FullName.ToUpperInvariant()}|{info.Length}|{info.LastWriteTimeUtc.Ticks}")));
            var directory = Path.Combine(AppDataPaths.Root, "video-frames");
            var target = Path.Combine(directory, key + ".jpg");
            if (File.Exists(target) && new FileInfo(target).Length > 0) return target;

            var ffmpeg = FfmpegExecutableLocator.Find(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH"));
            if (ffmpeg is null) return null;

            Directory.CreateDirectory(directory);
            Gate.Wait();
            try
            {
                if (File.Exists(target) && new FileInfo(target).Length > 0) return target;
                var temp = Path.Combine(directory, key + ".tmp.jpg");
                var start = new ProcessStartInfo(ffmpeg)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-i", info.FullName, "-frames:v", "1", "-vf", "scale=480:-2", "-q:v", "4", "-y", temp })
                    start.ArgumentList.Add(argument);
                using var process = Process.Start(start);
                if (process is null) return null;
                _ = process.StandardError.ReadToEndAsync();
                _ = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(15000))
                {
                    try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                    File.Delete(temp);
                    return null;
                }

                if (process.ExitCode != 0 || !File.Exists(temp) || new FileInfo(temp).Length == 0)
                {
                    if (File.Exists(temp)) File.Delete(temp);
                    return null;
                }

                File.Move(temp, target, overwrite: true);
                return target;
            }
            finally
            {
                Gate.Release();
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
}
