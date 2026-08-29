using System.IO;

namespace HanabePhotoManager.App.Services;

public sealed record ImportResumeTargetResolution(bool Success, string TargetDirectory, string ErrorMessage = "");

public static class ImportResumeTargetResolver
{
    public static ImportResumeTargetResolution Resolve(string libraryRoot, ImportResumeEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentNullException.ThrowIfNull(entry);

        if (!string.IsNullOrWhiteSpace(entry.TargetDateDirectory))
        {
            var persisted = Path.GetFullPath(entry.TargetDateDirectory);
            return Directory.Exists(persisted)
                ? new(true, persisted)
                : new(false, string.Empty, $"已确认目标文件夹不存在：{persisted}");
        }

        var candidates = LibraryDateFolderService.Scan(libraryRoot)
            .Where(folder => folder.Month == entry.Month && folder.Day == entry.Day)
            .Select(folder => folder.FullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return candidates.Length switch
        {
            1 => new(true, candidates[0]),
            0 => new(false, string.Empty, $"找不到旧记录对应的 {entry.Month:00}.{entry.Day:00} 日期文件夹。"),
            _ => new(false, string.Empty, $"{entry.Month:00}.{entry.Day:00} 存在多个日期文件夹，无法安全判断旧记录应继续到哪一个。"),
        };
    }
}
