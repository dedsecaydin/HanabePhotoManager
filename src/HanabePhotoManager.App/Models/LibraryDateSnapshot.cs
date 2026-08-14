namespace HanabePhotoManager.App.Models;

public sealed record LibraryDateMediaItem(
    string FullPath,
    string Name,
    string Extension,
    string Category,
    long Length,
    DateTime LastWriteTimeUtc);

public sealed record LibraryDateCategorySnapshot(
    string Name,
    string DirectoryPath,
    int FileCount,
    long TotalBytes);

public enum LibraryDateSnapshotWarningKind
{
    CategoryUnavailable,
    FileUnavailable,
    DirectoryChangedDuringScan,
    CapacityEntryUnavailable
}

public sealed record LibraryDateSnapshotWarning(
    LibraryDateSnapshotWarningKind Kind,
    string Path,
    string Message);

public sealed record LibraryDateSnapshot(
    string DateDirectory,
    IReadOnlyList<LibraryDateMediaItem> Items,
    IReadOnlyList<LibraryDateCategorySnapshot> Categories,
    IReadOnlyList<LibraryDateSnapshotWarning> Warnings,
    bool IsPartial,
    string Fingerprint,
    DateTime CreatedUtc);

public sealed record LibraryDateSnapshotBatch(
    IReadOnlyList<LibraryDateMediaItem> Items,
    int DiscoveredCount,
    bool FromCache);

public sealed record LibraryDirectoryCapacityResult(
    string DirectoryPath,
    long TotalBytes,
    int FilesVisited,
    IReadOnlyList<LibraryDateSnapshotWarning> Warnings,
    bool IsPartial);

public sealed record LibraryDateFileProperties(
    string FullPath,
    string Name,
    string Extension,
    long Length,
    DateTime LastWriteTimeUtc);

public sealed record LibraryDateFileReadResult(
    LibraryDateFileProperties? File,
    string? FailedPath,
    string? ErrorMessage)
{
    public bool IsSuccess => File is not null;

    public static LibraryDateFileReadResult Success(LibraryDateFileProperties file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new LibraryDateFileReadResult(file, null, null);
    }

    public static LibraryDateFileReadResult Failure(string path, string message) =>
        new(null, path, message);
}
