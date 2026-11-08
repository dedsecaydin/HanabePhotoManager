using System.IO;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.Imports;

/// <summary>为同名不同内容的媒体组寻找共同前缀，保留现有目标文件。</summary>
public static class ImportConflictNameResolver
{
    public static ImportPlanItem Resolve(ImportPlanItem item)
    {
        if (item.Conflict != ConflictKind.SameNameDifferentContent) return item;
        for (var number = 2; number <= 9999; number++)
        {
            var prefix = $"保留{number:00}_";
            var files = item.Files.Select(file =>
            {
                var destination = Path.Combine(Path.GetDirectoryName(file.DestinationPath)!,
                    prefix + Path.GetFileName(file.DestinationPath));
                return new PlannedFile(file.Source, destination, destination + ".hanabe-part", ConflictKind.None);
            }).ToArray();
            if (files.All(file => !File.Exists(file.DestinationPath) && !File.Exists(file.TemporaryPath)))
                return new ImportPlanItem(item.Id, item.Group, files, ConflictKind.None, item.State);
        }
        throw new IOException("同名保留文件已达到上限，无法为导入组生成安全名称。");
    }
}
