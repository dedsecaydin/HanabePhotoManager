namespace HanabePhotoManager.Core;

internal static class LocalPathSyntax
{
    public static bool IsFullyQualified(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        if (HasDriveRoot(normalized))
        {
            return true;
        }

        if (normalized.StartsWith("//", StringComparison.Ordinal))
        {
            return normalized
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Length >= 2;
        }

        return path.StartsWith("/", StringComparison.Ordinal);
    }

    public static string NormalizeIdentity(string path)
    {
        if (!IsFullyQualified(path))
        {
            throw new ArgumentException($"Path '{path}' must be fully qualified.", nameof(path));
        }

        var normalized = path.Replace('\\', '/');
        if (HasDriveRoot(normalized))
        {
            return NormalizeFromRoot(normalized[..3], normalized[3..]);
        }

        if (normalized.StartsWith("//", StringComparison.Ordinal))
        {
            var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var root = $"//{parts[0]}/{parts[1]}";
            return NormalizeFromRoot(root, string.Join('/', parts.Skip(2)));
        }

        return NormalizeFromRoot("/", normalized[1..]);
    }

    public static string GetFileName(string path)
    {
        var normalized = path.Replace('\\', '/');
        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex < 0 ? normalized : normalized[(separatorIndex + 1)..];
    }

    public static string GetFileNameWithoutExtension(string path)
    {
        return Path.GetFileNameWithoutExtension(GetFileName(path));
    }

    public static string GetDirectoryName(string normalizedPath)
    {
        var separatorIndex = normalizedPath.LastIndexOf('/');
        if (separatorIndex < 0)
        {
            return string.Empty;
        }

        if (separatorIndex == 0)
        {
            return "/";
        }

        if (separatorIndex == 2 && HasDriveRoot(normalizedPath))
        {
            return normalizedPath[..3];
        }

        return normalizedPath[..separatorIndex];
    }

    private static bool HasDriveRoot(string normalized)
    {
        return normalized.Length >= 3 &&
               IsAsciiLetter(normalized[0]) &&
               normalized[1] == ':' &&
               normalized[2] == '/';
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static string NormalizeFromRoot(string root, string remainder)
    {
        var segments = new List<string>();
        foreach (var segment in remainder.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
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

        if (segments.Count == 0)
        {
            return root;
        }

        var separator = root.EndsWith("/", StringComparison.Ordinal) ? string.Empty : "/";
        return $"{root}{separator}{string.Join('/', segments)}";
    }
}
