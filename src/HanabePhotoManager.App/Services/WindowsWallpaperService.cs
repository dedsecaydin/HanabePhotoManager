using System.IO;
using Microsoft.Win32;

namespace HanabePhotoManager.App.Services;

public interface IWindowsWallpaperService
{
    string? GetCurrentWallpaperPath();
}

public sealed class WindowsWallpaperService : IWindowsWallpaperService
{
    private readonly Func<string?> _readConfiguredPath;

    public WindowsWallpaperService() : this(ReadWindowsWallpaperPath)
    {
    }

    public WindowsWallpaperService(Func<string?> readConfiguredPath)
    {
        _readConfiguredPath = readConfiguredPath ?? throw new ArgumentNullException(nameof(readConfiguredPath));
    }

    public string? GetCurrentWallpaperPath()
    {
        var configuredPath = Environment.ExpandEnvironmentVariables(_readConfiguredPath()?.Trim() ?? string.Empty);
        if (configuredPath.Length == 0 || !File.Exists(configuredPath))
        {
            return null;
        }

        return Path.GetFullPath(configuredPath);
    }

    private static string? ReadWindowsWallpaperPath()
    {
        using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var configured = desktop?.GetValue("WallPaper") as string;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var transcoded = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
        return File.Exists(transcoded) ? transcoded : null;
    }
}
