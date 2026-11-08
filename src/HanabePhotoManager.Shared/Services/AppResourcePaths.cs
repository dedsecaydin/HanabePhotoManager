using System.IO;

namespace HanabePhotoManager.App.Services;

/// <summary>Finds resources in a macOS bundle or beside a standalone application.</summary>
public static class AppResourcePaths
{
    public static string Find(string baseDirectory, params string[] relativeParts)
    {
        var basePath = Path.GetFullPath(baseDirectory);
        var folder = new DirectoryInfo(basePath);
        if (string.Equals(folder.Name, "MacOS", StringComparison.Ordinal) &&
            string.Equals(folder.Parent?.Name, "Contents", StringComparison.Ordinal))
        {
            var parts = new[] { folder.Parent!.FullName, "Resources" }.Concat(relativeParts).ToArray();
            var bundled = Path.Combine(parts);
            if (File.Exists(bundled) || Directory.Exists(bundled)) return bundled;
        }
        return Path.Combine(new[] { basePath }.Concat(relativeParts).ToArray());
    }
}
