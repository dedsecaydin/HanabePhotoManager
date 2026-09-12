using System.Globalization;
using System.IO;

namespace HanabePhotoManager.App.Recovery;

internal static class RecoveryOutputDirectory
{
    public static void Prepare(string directory)
    {
        Validate(directory);
        Directory.CreateDirectory(directory);
        Validate(directory);
        using var probe = new FileStream(Path.Combine(directory, ".write-check-" + Guid.NewGuid().ToString("N")),
            FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        probe.WriteByte(0);
        probe.Flush(true);
    }
    public static string CreatePath(string source, DateTime localTime, string root = @"D:\HanabeRecovery")
    {
        var name = Path.GetFileNameWithoutExtension(source.TrimEnd('\\', '/'));
        name = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
        if (string.IsNullOrEmpty(name)) name = "存储卡";
        if (name.Length > 48) name = name[..48];
        return Path.Combine(Path.GetFullPath(root), localTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            $"{localTime:HHmmss}_{name}_{Guid.NewGuid():N}");
    }

    public static void Validate(string directory, long requiredBytes = 0)
    {
        if (requiredBytes < 0) throw new ArgumentOutOfRangeException(nameof(requiredBytes));
        var full = Path.GetFullPath(directory);
        var drive = new DriveInfo(Path.GetPathRoot(full)!);
        if (!drive.IsReady) throw new IOException("恢复输出盘不可用，请检查 D 盘连接。");
        if (drive.AvailableFreeSpace < requiredBytes) throw new IOException("恢复输出盘空间不足，无法同时保存媒体副本、原始候选和报告。");
        // Reject junctions/symlinks so an apparently local destination cannot redirect writes.
        for (var parent = new DirectoryInfo(full); parent is not null; parent = parent.Parent)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("恢复输出目录不能通过链接或目录联接重定向。");
    }
}
