using System.IO;
using System.Runtime.InteropServices;

namespace HanabePhotoManager.App.Platform;

internal sealed class WindowsStorageCapacityProvider : IStorageCapacityProvider
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(string directory, out ulong freeForUser, out ulong totalBytes, out ulong totalFree);

    public (ulong Free, ulong Total)? GetCapacity(string path)
    {
        var directory = path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
        return GetDiskFreeSpaceEx(directory, out var free, out var total, out _) && total > 0 ? (free, total) : null;
    }
}
