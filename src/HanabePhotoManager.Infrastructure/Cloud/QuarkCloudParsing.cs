using System.Text.Json;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Infrastructure.Cloud;

/// <summary>
/// 夸克 CLI NDJSON 解析辅助：把 quark-drive.cjs 输出的条目（BrowseFileItem 等）
/// 映射为领域模型，并提供 JSON 容错读取。与 QuarkCloudProvider 拆分以控制文件行数。
/// </summary>
internal static class QuarkCloudParsing
{
    /// <summary>把 search 的条目（含 artifact 落盘条目）映射为 CloudObject；无法映射返回 false。</summary>
    public static bool TryMap(JsonElement entry, string requestedDirectory, out CloudObject mapped)
    {
        mapped = null!;

        var fid = GetString(entry, "fid");
        if (string.IsNullOrWhiteSpace(fid))
        {
            return false;
        }

        var name = GetString(entry, "filename");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = fid;
        }

        var isFolder = TryGetInt32(entry, "category", out var category) && category == 0;

        long size = 0;
        if (TryGetInt64(entry, "size", out var sizeValue) && sizeValue > 0)
        {
            size = sizeValue;
        }

        var modifiedAt = DateTimeOffset.UtcNow;
        if (TryGetInt64(entry, "updated_at", out var updatedAt) && updatedAt > 0)
        {
            modifiedAt = DateTimeOffset.FromUnixTimeMilliseconds(updatedAt);
        }
        else if (TryGetInt64(entry, "created_at", out var createdAt) && createdAt > 0)
        {
            modifiedAt = DateTimeOffset.FromUnixTimeMilliseconds(createdAt);
        }

        var path = TryCreatePath(entry, name, requestedDirectory);
        if (path is null)
        {
            return false;
        }

        // 有真实路径时只返回请求目录下的条目；无路径信息时仅在根目录全量返回。
        if (requestedDirectory != "/" && !IsInside(path.Value, requestedDirectory))
        {
            return false;
        }

        mapped = new CloudObject(
            CloudProviderKind.Quark,
            fid,
            path,
            name,
            isFolder ? CloudObjectKind.Folder : GetObjectKind(name),
            size,
            modifiedAt,
            null,   // thumbnailKey：CLI 无缩略图读取接口
            false); // isHanabeManaged
        return true;
    }

    /// <summary>优先用条目的真实 path，缺失时按请求目录合成。</summary>
    public static CloudPath? TryCreatePath(JsonElement entry, string name, string requestedDirectory)
    {
        var raw = GetString(entry, "path");
        if (!string.IsNullOrWhiteSpace(raw) &&
            TryCreateCloudPath(NormalizeQuarkPath(raw), out var path))
        {
            return path;
        }

        var combined = requestedDirectory == "/"
            ? $"/{name}"
            : $"{requestedDirectory.TrimEnd('/')}/{name}";
        return TryCreateCloudPath(combined, out var synthesized) ? synthesized : null;
    }

    public static bool TryCreateCloudPath(string value, out CloudPath path)
    {
        try
        {
            path = new CloudPath(value);
            return true;
        }
        catch (ArgumentException)
        {
            path = null!;
            return false;
        }
    }

    /// <summary>统一路径格式：补前导斜杠，剥掉夸克返回路径中的"夸克网盘"驱动根。</summary>
    public static string NormalizeQuarkPath(string raw)
    {
        var normalized = raw.Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && segments[0] == "夸克网盘")
        {
            normalized = "/" + string.Join('/', segments.Skip(1));
        }

        return normalized;
    }

    public static bool IsInside(string pathValue, string directory)
    {
        if (directory == "/")
        {
            return true;
        }

        var prefix = directory.TrimEnd('/');
        return pathValue.StartsWith(prefix + "/", StringComparison.Ordinal);
    }

    /// <summary>读取 search 命令 artifact 落盘 jsonl（每行一个 BrowseFileItem）。</summary>
    public static IEnumerable<JsonElement> ReadArtifactEntries(string filePath)
    {
        var entries = new List<JsonElement>();
        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(line);
                    entries.Add(document.RootElement.Clone());
                }
                catch (JsonException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        foreach (var entry in entries)
        {
            yield return entry;
        }
    }

    /// <summary>在 search result 的 file_list 中查找指定 FID。</summary>
    public static bool SearchContainsFid(JsonElement data, string remoteId)
    {
        if (!TryGetProperty(data, "file_list", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var entry in list.EnumerateArray())
        {
            if (string.Equals(GetString(entry, "fid"), remoteId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>在 upload 输出的 list 行中查找与上传文件名匹配的 fileId。</summary>
    public static string? FindUploadedFid(QuarkCliOutput output, string fileName)
    {
        foreach (var line in output.Lines)
        {
            if (line.Type != "list" || line.Code != 0)
            {
                continue;
            }

            var lineName = TryGetProperty(line.Data, "fileName", out var nameProperty)
                ? nameProperty.GetString()
                : null;
            var fileId = TryGetProperty(line.Data, "fileId", out var fileIdProperty)
                ? fileIdProperty.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(fileId) &&
                (lineName == fileName || string.IsNullOrWhiteSpace(lineName)))
            {
                return fileId;
            }
        }

        return null;
    }

    /// <summary>取 upload 汇总 result 行的第一个 FID。</summary>
    public static string? ReadFirstFid(JsonElement data)
    {
        if (!TryGetProperty(data, "fids", out var fids) ||
            fids.ValueKind != JsonValueKind.Array ||
            fids.GetArrayLength() == 0 ||
            fids[0].ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return fids[0].GetString();
    }

    /// <summary>把 upload 的 progress 行转为 CloudUploadProgress 上报。</summary>
    public static void ReportUploadProgress(
        QuarkCliLine line,
        IProgress<CloudUploadProgress>? progress,
        string fileName)
    {
        if (progress is null || line.Type != "progress")
        {
            return;
        }

        if (!TryGetInt64(line.Data, "current", out var current) ||
            !TryGetInt64(line.Data, "total", out var total) ||
            total <= 0)
        {
            return;
        }

        progress.Report(new CloudUploadProgress(
            Math.Clamp(current, 0, total),
            total,
            fileName));
    }

    public static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property))
        {
            value = property;
            return true;
        }

        value = default;
        return false;
    }

    public static string? GetString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static bool TryGetInt64(JsonElement element, string name, out long value)
    {
        if (TryGetProperty(element, name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt64(out var parsed))
        {
            value = parsed;
            return true;
        }

        value = 0;
        return false;
    }

    public static bool TryGetInt32(JsonElement element, string name, out int value)
    {
        if (TryGetProperty(element, name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var parsed))
        {
            value = parsed;
            return true;
        }

        value = 0;
        return false;
    }

    public static CloudObjectKind GetObjectKind(string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tif" or ".tiff" or ".webp")
        {
            return CloudObjectKind.Image;
        }

        if (extension is ".arw" or ".cr2" or ".cr3" or ".nef" or ".raf" or ".rw2" or ".orf" or ".dng" or ".raw")
        {
            return CloudObjectKind.Raw;
        }

        if (extension is ".mp4" or ".mov" or ".m4v" or ".avi" or ".mkv" or ".wmv")
        {
            return CloudObjectKind.Video;
        }

        if (extension is ".aac" or ".wav" or ".mp3" or ".m4a" or ".flac" or ".ogg")
        {
            return CloudObjectKind.Audio;
        }

        return CloudObjectKind.Other;
    }
}
