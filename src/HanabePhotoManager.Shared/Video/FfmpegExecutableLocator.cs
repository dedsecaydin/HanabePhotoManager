using System.IO;

namespace HanabePhotoManager.App.Video;

internal static class FfmpegExecutableLocator
{
    public static string Resolve() => Find(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH"))
        ?? throw new FileNotFoundException("未找到 FFmpeg。请将 FFmpeg 放在程序目录的 tools 文件夹内，或安装后加入 PATH。");

    internal static string? Find(string applicationDirectory, string? searchPath, bool? windows = null)
    {
        var directories = new[] { Path.Combine(applicationDirectory, "tools"), applicationDirectory }
            .Concat((searchPath ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => path.Trim().Trim('"')));
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) continue;
            var executable = Path.Combine(directory, (windows ?? OperatingSystem.IsWindows()) ? "ffmpeg.exe" : "ffmpeg");
            if (File.Exists(executable)) return Path.GetFullPath(executable);
        }
        return null;
    }

    internal static string ProbePath(string ffmpeg, bool? windows = null) =>
        Path.Combine(Path.GetDirectoryName(ffmpeg)!, (windows ?? OperatingSystem.IsWindows()) ? "ffprobe.exe" : "ffprobe");
}
