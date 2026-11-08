using System.Collections.Concurrent;
using System.Text.Json;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>兼容旧图库标记文件的分类、标签和评分数据。</summary>
public sealed class FileMeta
{
    public int Rating { get; set; }
    public string Tags { get; set; } = "";
}

/// <summary>读取和写入旧版单文件元数据，供迁移与兼容使用。</summary>
public static class FileMetaStore
{
    private static readonly ConcurrentDictionary<string, FileMeta> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static FileMeta TryGet(string filePath)
    {
        if (Cache.TryGetValue(filePath, out var cached)) return cached;

        var metaPath = GetMetaPath(filePath);
        if (metaPath is null) return new();

        try
        {
            if (File.Exists(metaPath))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, FileMeta>>(File.ReadAllText(metaPath));
                if (dict is not null && dict.TryGetValue(filePath, out var meta))
                {
                    Cache[filePath] = meta;
                    return meta;
                }
            }
        }
        catch { }

        var empty = new FileMeta();
        Cache[filePath] = empty;
        return empty;
    }

    public static void Update(string filePath, Action<FileMeta> mutate)
    {
        var current = TryGet(filePath);
        mutate(current);
        Cache[filePath] = current;

        var metaPath = GetMetaPath(filePath);
        if (metaPath is null) return;

        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
                var dict = File.Exists(metaPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, FileMeta>>(File.ReadAllText(metaPath)) ?? new()
                    : new();
                dict[filePath] = current;
                File.WriteAllText(metaPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { }
        }
    }

    private static string? GetMetaPath(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        return dir is not null ? Path.Combine(dir, ".hanabe", "meta.json") : null;
    }
}

