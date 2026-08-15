using HanabePhotoManager.Desktop.Core.Platform;

namespace HanabePhotoManager.Desktop.Platform;

public sealed class MacOsAppPaths : IAppPaths
{
    public MacOsAppPaths()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("MacOsAppPaths can only be used on macOS.");
        }

        var paths = MacOsAppPathsPolicy.Resolve(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        Directory.CreateDirectory(paths.ApplicationDataDirectory);
        Directory.CreateDirectory(paths.CacheDirectory);

        ApplicationDataDirectory = paths.ApplicationDataDirectory;
        CacheDirectory = paths.CacheDirectory;
    }

    public string ApplicationDataDirectory { get; }

    public string CacheDirectory { get; }
}
