namespace HanabePhotoManager.Desktop.Core.Platform;

public static class MacOsCommandPolicy
{
    public static ProcessCommand MoveToTrash(string path)
    {
        var normalizedPath = NormalizePath(path);

        return new ProcessCommand(
            "/usr/bin/osascript",
            [
                "-e",
                "on run argv",
                "-e",
                "tell application \"Finder\" to delete POSIX file (item 1 of argv)",
                "-e",
                "end run",
                "--",
                normalizedPath
            ]);
    }

    public static ProcessCommand Reveal(string path)
    {
        var normalizedPath = NormalizePath(path);

        return new ProcessCommand("/usr/bin/open", ["-R", normalizedPath]);
    }

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (path.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("The path cannot contain a null character.", nameof(path));
        }

        if (OperatingSystem.IsWindows() && path.StartsWith("/", StringComparison.Ordinal))
        {
            return NormalizePosixPath(path);
        }

        return Path.GetFullPath(path);
    }

    private static string NormalizePosixPath(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.None))
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
