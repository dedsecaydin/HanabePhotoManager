using System.Text.Json;

namespace HanabePhotoManager.Infrastructure.Files;

public sealed record QuarantinedFile(string Id, string OriginalPath, long Length, DateTimeOffset IsolatedAt);

/// <summary>Journal-first, same-volume isolation. A failed final move leaves the original or payload recoverable.</summary>
public sealed class LibraryQuarantineService
{
    public const string DirectoryName = ".hanabe-quarantine";

    public async Task QuarantineAsync(string libraryRoot, string path, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(libraryRoot);
        var original = Path.GetFullPath(path);
        ValidateOriginal(root, original);
        if (RetouchedDirectoryPolicy.IsReadOnlyRetouchedPath(root, original))
            throw new IOException("已修目录为只读，不能隔离。");
        var id = Guid.NewGuid().ToString("N");
        var folder = Path.Combine(root, DirectoryName, id);
        Directory.CreateDirectory(folder);
        var entry = new QuarantinedFile(id, original, new FileInfo(original).Length, DateTimeOffset.UtcNow);
        await using (var journal = new FileStream(Path.Combine(folder, "entry.json"), FileMode.CreateNew,
                         FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(journal, entry, cancellationToken: cancellationToken);
            await journal.FlushAsync(cancellationToken);
            journal.Flush(true);
        }
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(original, Path.Combine(folder, "content.payload"), false);
    }

    public IReadOnlyList<QuarantinedFile> List(string libraryRoot)
    {
        var root = Path.GetFullPath(libraryRoot);
        var storage = Path.Combine(root, DirectoryName);
        if (!Directory.Exists(storage)) return [];
        var entries = new List<QuarantinedFile>();
        foreach (var folder in Directory.EnumerateDirectories(storage))
        {
            if (!File.Exists(Path.Combine(folder, "content.payload"))) continue;
            var entry = JsonSerializer.Deserialize<QuarantinedFile>(File.ReadAllText(Path.Combine(folder, "entry.json")))
                ?? throw new IOException("隔离记录无法读取。");
            ValidateEntry(root, entry);
            if (!string.Equals(Path.GetFileName(folder), entry.Id, StringComparison.Ordinal))
                throw new IOException("隔离记录与目录不匹配。");
            entries.Add(entry);
        }
        return entries.OrderByDescending(entry => entry.IsolatedAt).ToArray();
    }

    public void Restore(string libraryRoot, QuarantinedFile entry)
    {
        var root = Path.GetFullPath(libraryRoot);
        ValidateEntry(root, entry);
        var folder = Path.Combine(root, DirectoryName, entry.Id);
        var recorded = JsonSerializer.Deserialize<QuarantinedFile>(File.ReadAllText(Path.Combine(folder, "entry.json")));
        if (recorded != entry) throw new IOException("隔离记录已改变，请刷新后重试。");
        if (File.Exists(entry.OriginalPath) || Directory.Exists(entry.OriginalPath))
            throw new IOException("原路径已有内容，未覆盖。请先移开冲突文件再重试。");
        Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath)!);
        File.Move(Path.Combine(folder, "content.payload"), entry.OriginalPath, false);
        // Keep the journal as an audit record; List excludes entries without a payload.
    }

    private static void ValidateEntry(string root, QuarantinedFile entry)
    {
        if (!Guid.TryParseExact(entry.Id, "N", out _)) throw new IOException("隔离记录编号无效。");
        ValidateOriginal(root, entry.OriginalPath);
    }

    private static void ValidateOriginal(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar)
            || relative == "." || relative.Split(Path.DirectorySeparatorChar)[0].Equals(DirectoryName, StringComparison.OrdinalIgnoreCase))
            throw new IOException("只能处理当前照片库内的原始文件。");
        // Junctions could redirect a library-relative path outside the library.
        for (var current = Path.GetFullPath(path); !string.Equals(current, root, StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current)!)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("不能通过链接目录隔离或恢复文件。");
        }
        var storage = Path.Combine(root, DirectoryName);
        if (Directory.Exists(storage) && (File.GetAttributes(storage) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("隔离目录不能是链接目录。");
    }
}
