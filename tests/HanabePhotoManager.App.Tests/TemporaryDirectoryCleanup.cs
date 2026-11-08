using System.IO;

namespace HanabePhotoManager.App.Tests;

internal static class TemporaryDirectoryCleanup
{
    // Windows may briefly deny deletion while another process scans newly written images.
    // Keep persistent locks visible as test failures instead of silently leaving data behind.
    public static void Delete(string path)
    {
        var root = Path.GetFullPath(@"D:\HanabePhoto\.artifacts").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        path = Path.GetFullPath(path);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Test cleanup must remain inside project artifacts.");
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 20) { Thread.Sleep(100); }
        }
    }
}
