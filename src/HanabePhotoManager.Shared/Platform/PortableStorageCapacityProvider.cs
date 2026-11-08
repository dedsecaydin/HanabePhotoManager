using System.IO;

namespace HanabePhotoManager.App.Platform;

/// <summary>Reports capacity of the most specific mounted filesystem containing a directory.</summary>
public sealed class PortableStorageCapacityProvider : IStorageCapacityProvider
{
    public (ulong Free, ulong Total)? GetCapacity(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return null;
            static string WithSeparator(string value) => Path.EndsInDirectorySeparator(value) ? value : value + Path.DirectorySeparatorChar;
            var fullPath = WithSeparator(Path.GetFullPath(path));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var drive = DriveInfo.GetDrives()
                .Where(item => fullPath.StartsWith(WithSeparator(item.RootDirectory.FullName), comparison))
                .OrderByDescending(item => item.RootDirectory.FullName.Length)
                .FirstOrDefault();
            if (drive is null || !drive.IsReady || drive.TotalSize <= 0 || drive.AvailableFreeSpace < 0) return null;
            return ((ulong)drive.AvailableFreeSpace, (ulong)drive.TotalSize);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
