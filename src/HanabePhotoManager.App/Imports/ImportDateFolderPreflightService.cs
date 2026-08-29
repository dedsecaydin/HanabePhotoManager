using System.IO;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.Imports;

public sealed class ImportDateFolderPreflightService
{
    private string _libraryRoot = string.Empty;

    public IReadOnlyList<ImportDateFolderDecision> CreateDecisions(
        string libraryRoot,
        IEnumerable<(LibraryDate Date, int MediaCount)> dateGroups)
    {
        _libraryRoot = Path.GetFullPath(libraryRoot);
        var entries = LibraryDateFolderService.Scan(_libraryRoot);
        var decisions = dateGroups
            .OrderBy(group => group.Date.Year)
            .ThenBy(group => group.Date.Month)
            .ThenBy(group => group.Date.Day)
            .Select(group =>
            {
                var candidates = entries
                    .Where(entry => entry.Month == group.Date.Month && entry.Day == group.Date.Day)
                    .Select(entry => new ImportDateFolderCandidate(Path.GetFileName(entry.FullPath), entry.FullPath, entry.Remark))
                    .ToArray();
                var decision = new ImportDateFolderDecision(group.Date, group.MediaCount, candidates, Refresh);
                Refresh(decision);
                return decision;
            })
            .ToArray();
        return decisions;
    }

    public void Refresh(ImportDateFolderDecision decision)
    {
        var normalizedRemark = LibraryDateFolderService.NormalizeRemarkForFolderName(decision.Remark);
        decision.IsValid = false;
        decision.ValidationMessage = string.Empty;
        decision.FinalDirectoryName = string.Empty;
        decision.FinalDirectoryPath = string.Empty;

        if (decision.HasExistingFolders && decision.Strategy == ImportDateFolderStrategy.Unselected)
        {
            decision.ValidationMessage = "该日期已有文件夹，请选择处理方式。";
            decision.PublishResolution();
            return;
        }

        if (decision.Strategy is ImportDateFolderStrategy.RenameExisting or ImportDateFolderStrategy.UseExisting &&
            decision.SelectedExistingFolder is null)
        {
            decision.ValidationMessage = "请选择要使用的旧文件夹。";
            decision.PublishResolution();
            return;
        }

        if (decision.Strategy == ImportDateFolderStrategy.UseExisting)
        {
            decision.FinalDirectoryName = decision.SelectedExistingFolder!.Name;
            decision.FinalDirectoryPath = decision.SelectedExistingFolder.FullPath;
            decision.IsValid = Directory.Exists(decision.FinalDirectoryPath);
            decision.ValidationMessage = decision.IsValid ? string.Empty : "所选旧文件夹已不存在。";
            decision.PublishResolution();
            return;
        }

        if (decision.HasExistingFolders && decision.Strategy == ImportDateFolderStrategy.CreateSeparate && string.IsNullOrEmpty(normalizedRemark))
        {
            decision.ValidationMessage = "新建不同文件夹时请输入不同备注。";
            decision.PublishResolution();
            return;
        }

        var name = $"{decision.Date.Month:00}.{decision.Date.Day:00}" +
                   (string.IsNullOrEmpty(normalizedRemark) ? string.Empty : $"_{normalizedRemark}");
        var target = Path.Combine(_libraryRoot, $"{decision.Date.Month}月", name);
        decision.FinalDirectoryName = name;
        decision.FinalDirectoryPath = target;

        if (decision.Strategy == ImportDateFolderStrategy.RenameExisting)
        {
            if (!Directory.Exists(decision.SelectedExistingFolder!.FullPath))
            {
                decision.ValidationMessage = "所选旧文件夹已不存在。";
            }
            else if (!string.Equals(target, decision.SelectedExistingFolder.FullPath, StringComparison.OrdinalIgnoreCase) &&
                     (Directory.Exists(target) || File.Exists(target)))
            {
                decision.ValidationMessage = "目标文件夹已存在。";
            }
            else
            {
                decision.IsValid = true;
            }
        }
        else if (Directory.Exists(target) || File.Exists(target))
        {
            decision.ValidationMessage = "目标文件夹已存在，请更换备注。";
        }
        else
        {
            decision.IsValid = true;
        }

        decision.PublishResolution();
    }

    public bool ApplyRequiredRenames(IReadOnlyList<ImportDateFolderDecision> decisions)
    {
        foreach (var decision in decisions) Refresh(decision);
        if (decisions.Any(decision => !decision.IsValid)) return false;

        foreach (var decision in decisions.Where(decision => decision.Strategy == ImportDateFolderStrategy.RenameExisting))
        {
            var result = LibraryDateFolderService.RenameRemark(decision.SelectedExistingFolder!.FullPath, decision.Remark);
            if (result.Status is not (DateFolderRenameStatus.Success or DateFolderRenameStatus.NoChange))
            {
                decision.ValidationMessage = result.Status switch
                {
                    DateFolderRenameStatus.TargetExists => "目标文件夹已存在。",
                    DateFolderRenameStatus.SourceMissing => "所选旧文件夹已不存在。",
                    _ => "修改旧文件夹失败：" + result.ErrorMessage,
                };
                decision.IsValid = false;
                decision.PublishResolution();
                return false;
            }

            decision.FinalDirectoryPath = result.EffectivePath;
            decision.FinalDirectoryName = Path.GetFileName(result.EffectivePath);
            decision.PublishResolution();
        }

        return true;
    }
}
