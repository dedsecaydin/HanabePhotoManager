namespace HanabePhotoManager.Desktop.Core.Platform;

public static class MacOsAppPathsPolicy
{
    public static (string ApplicationDataDirectory, string CacheDirectory) Resolve(string homeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);

        var home = homeDirectory.TrimEnd('/', '\\');
        var library = $"{home}/Library";

        return (
            $"{library}/Application Support/Hanabe Photo Manager",
            $"{library}/Caches/Hanabe Photo Manager");
    }
}
