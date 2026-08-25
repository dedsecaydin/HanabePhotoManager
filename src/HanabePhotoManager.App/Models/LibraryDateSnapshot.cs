namespace HanabePhotoManager.App.Models;

/// <summary>日期快照中的单个媒体文件及其读取属性。</summary>
public sealed record LibraryDateMediaItem(
    string FullPath,
    string Name,
    string Extension,
    string Category,
    long Length,
    DateTime LastWriteTimeUtc);

/// <summary>一个日期分类目录中的媒体集合。</summary>
public sealed record LibraryDateCategorySnapshot(
    string Name,
    string DirectoryPath,
    int FileCount,
    long TotalBytes);

/// <summary>日期扫描期间可恢复警告的类别。</summary>
public enum LibraryDateSnapshotWarningKind
{
    CategoryUnavailable,
    FileUnavailable,
    DirectoryChangedDuringScan,
    CapacityEntryUnavailable
}

/// <summary>扫描路径、警告类别和可展示原因。</summary>
public sealed record LibraryDateSnapshotWarning(
    LibraryDateSnapshotWarningKind Kind,
    string Path,
    string Message);

/// <summary>单个日期目录的分类媒体、标题和警告快照。</summary>
public sealed record LibraryDateSnapshot(
    string DateDirectory,
    IReadOnlyList<LibraryDateMediaItem> Items,
    IReadOnlyList<LibraryDateCategorySnapshot> Categories,
    IReadOnlyList<LibraryDateSnapshotWarning> Warnings,
    bool IsPartial,
    string Fingerprint,
    DateTime CreatedUtc);

/// <summary>一次照片库扫描返回的全部日期快照和容量信息。</summary>
public sealed record LibraryDateSnapshotBatch(
    IReadOnlyList<LibraryDateMediaItem> Items,
    int DiscoveredCount,
    bool FromCache);

/// <summary>目录剩余空间查询结果及失败原因。</summary>
public sealed record LibraryDirectoryCapacityResult(
    string DirectoryPath,
    long TotalBytes,
    int FilesVisited,
    IReadOnlyList<LibraryDateSnapshotWarning> Warnings,
    bool IsPartial);

/// <summary>用于增量判断的文件大小和最后修改时间。</summary>
public sealed record LibraryDateFileProperties(
    string FullPath,
    string Name,
    string Extension,
    long Length,
    DateTime LastWriteTimeUtc);

/// <summary>文件属性读取结果；失败时携带可恢复警告。</summary>
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
