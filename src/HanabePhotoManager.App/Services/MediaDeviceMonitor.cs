using System.IO;

namespace HanabePhotoManager.App.Services;

/// <summary>Read-only removable-volume discovery. Each insertion is offered once.</summary>
internal sealed class MediaDeviceMonitor
{
    private readonly HashSet<string> _offered = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".heic", ".tif", ".tiff", ".arw", ".cr2", ".cr3", ".nef", ".dng", ".raf", ".orf", ".rw2", ".mp4", ".mov", ".m4v", ".mts", ".m2ts" };

    internal IReadOnlyList<string> Discover(CancellationToken token)
    {
        var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable).ToArray();
        var ready = new List<string>();
        foreach (var drive in drives)
        {
            token.ThrowIfCancellationRequested();
            try { if (drive.IsReady) ready.Add(drive.RootDirectory.FullName); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        _offered.IntersectWith(ready);
        var found = new List<string>();
        foreach (var root in ready.Where(root => !_offered.Contains(root)))
            if (ContainsMedia(root, token)) { _offered.Add(root); found.Add(root); }
        return found;
    }

    internal static bool ContainsMedia(string root, CancellationToken token)
    {
        var pending = new Queue<(string Path, int Depth)>(); pending.Enqueue((root, 0));
        int visited = 0;
        while (pending.Count > 0 && visited < 10000)
        {
            token.ThrowIfCancellationRequested();
            var (path, depth) = pending.Dequeue();
            try
            {
                foreach (var item in new DirectoryInfo(path).EnumerateFileSystemInfos())
                {
                    token.ThrowIfCancellationRequested();
                    if (++visited > 10000) break;
                    if ((item.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if (item is FileInfo file && file.Length > 0 && Extensions.Contains(file.Extension)) return true;
                    if (item is DirectoryInfo && depth < 8) pending.Enqueue((item.FullName, depth + 1));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return false;
    }
}
