namespace HanabePhotoManager.Desktop.Core.Platform;

public static class MacOsAppPathsPolicy
{
    public static (string ApplicationDataDirectory, string CacheDirectory) Resolve(string homeDirectory)
    {
        var home = NormalizeHomeDirectory(homeDirectory);
        var library = home == "/" ? "/Library" : $"{home}/Library";

        return (
            $"{library}/Application Support/Hanabe Photo Manager",
            $"{library}/Caches/Hanabe Photo Manager");
    }

    private static string NormalizeHomeDirectory(string homeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);

        if (homeDirectory.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("The home directory cannot contain a null character.", nameof(homeDirectory));
        }

        if (!homeDirectory.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("The home directory must be an absolute POSIX path.", nameof(homeDirectory));
        }

        var segments = new List<string>();
        foreach (var segment in homeDirectory.Split('/', StringSplitOptions.None))
        {
            if (segment is "" or ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            segments.Add(segment);
        }

        return segments.Count == 0 ? "/" : $"/{string.Join('/', segments)}";
    }
}
