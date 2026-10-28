using System.IO;

namespace HanabePhotoManager.App.Video;

internal static class FfmpegExecutableLocator
{
    public static string Resolve() => Find(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH"))
        ?? throw new FileNotFoundException("未找到 FFmpeg。请将 ffmpeg.exe 放在程序目录的 tools 文件夹内，或重新复制完整程序包。");

    internal static string? Find(string applicationDirectory, string? searchPath)
    {
        var directories = new[] { Path.Combine(applicationDirectory, "tools"), applicationDirectory }
            .Concat((searchPath ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => path.Trim().Trim('"')));
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) continue;
            var executable = Path.Combine(directory, "ffmpeg.exe");
            if (File.Exists(executable)) return Path.GetFullPath(executable);
        }
        return null;
    }
}
