using System.IO;

namespace HanabePhotoManager.Core.Albums;

public sealed record CustomAlbum(Guid Id, string DisplayName, string FolderPath)
{
    public static CustomAlbum Create(Guid id, string? displayName, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("A folder path is required.", nameof(folderPath));
        }

        var normalizedPath = Path.GetFullPath(folderPath);
        var normalizedName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : displayName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("An album display name is required.", nameof(displayName));
        }

        return new CustomAlbum(id == Guid.Empty ? Guid.NewGuid() : id, normalizedName, normalizedPath);
    }
}
