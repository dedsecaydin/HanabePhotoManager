using System.IO;

namespace HanabePhotoManager.Mac.Services;

public sealed record ImportVolume(string Name, string Path, string Detail);

/// <summary>
/// 列出可导入的存储卡 / U 盘 / 相机。
/// macOS：/Volumes 下除系统启动盘以外的卷（插卡后 Finder 挂载在这里）；
/// Windows（调试）：可移动磁盘。
/// </summary>
public static class VolumeService
{
    public static IReadOnlyList<ImportVolume> List(string? libraryRoot)
    {
        var result = new List<ImportVolume>();
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                foreach (var dir in Directory.EnumerateDirectories("/Volumes"))
                {
                    var info = new DirectoryInfo(dir);
                    // 启动盘在 /Volumes 下是指向 / 的符号链接
                    if (info.LinkTarget is "/" or "/System/Volumes/Data") continue;
                    if (IsInside(libraryRoot, dir)) continue;
                    result.Add(new ImportVolume(info.Name, dir, Describe(dir)));
                }
            }
            else
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Removable))
                {
                    if (IsInside(libraryRoot, drive.RootDirectory.FullName)) continue;
                    var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "可移动磁盘" : drive.VolumeLabel;
                    result.Add(new ImportVolume($"{label} ({drive.Name.TrimEnd('\\')})", drive.RootDirectory.FullName, Describe(drive.RootDirectory.FullName)));
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return result;
    }

    private static string Describe(string root)
    {
        var hints = new[] { "DCIM", "PRIVATE", "AVF_INFO", "MP_ROOT" }.Where(name => Directory.Exists(System.IO.Path.Combine(root, name))).ToArray();
        return hints.Length > 0 ? "相机存储卡 · " + string.Join(" / ", hints) : root;
    }

    internal static bool IsInside(string? parent, string child)
    {
        if (string.IsNullOrWhiteSpace(parent)) return false;
        var library = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        var volume = Path.TrimEndingDirectorySeparator(Path.GetFullPath(child));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(library, volume, comparison)) return true;
        var prefix = Path.EndsInDirectorySeparator(volume) ? volume : volume + Path.DirectorySeparatorChar;
        return library.StartsWith(prefix, comparison);
    }
}
