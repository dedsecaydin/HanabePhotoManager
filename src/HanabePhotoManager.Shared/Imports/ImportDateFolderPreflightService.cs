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

    /// <summary>
    /// 同一天分段版本：每个日期给出已经切好的段（1 段 = 整天一个文件夹）。
    /// 当天已有文件夹时第 1 段默认用原文件夹，其余段默认新建并自动填“上午 / 下午 / 晚上”。
    /// </summary>
    public IReadOnlyList<ImportDateFolderDecision> CreateSegmentedDecisions(
        string libraryRoot,
        IEnumerable<ImportDayPlan> days)
    {
        _libraryRoot = Path.GetFullPath(libraryRoot);
        var entries = LibraryDateFolderService.Scan(_libraryRoot);
        var decisions = new List<ImportDateFolderDecision>();
        foreach (var day in days.OrderBy(day => day.Date.Year).ThenBy(day => day.Date.Month).ThenBy(day => day.Date.Day))
        {
            var candidates = entries
                .Where(entry => entry.Month == day.Date.Month && entry.Day == day.Date.Day)
                .Select(entry => new ImportDateFolderCandidate(Path.GetFileName(entry.FullPath), entry.FullPath, entry.Remark))
                .ToArray();
            if (day.Segments.Count <= 1)
            {
                var whole = new ImportDateFolderDecision(day.Date, day.MediaCount, candidates, Refresh);
                whole.ApplySegment(day.Segments.FirstOrDefault(), 1, day.AutoSegmentCount, day.KeptTogether, []);
                Refresh(whole);
                decisions.Add(whole);
                continue;
            }

            // 新建段的默认备注：与当天已有文件夹的备注、以及彼此之间都不重名。
            var newSegments = day.Segments.Where(segment => candidates.Length == 0 || segment.Index > 0).ToArray();
            var existingRemarks = candidates.Select(candidate => candidate.Remark).Where(remark => remark.Length > 0).ToArray();
            var remarks = ImportDaySplitPlanner.UniqueRemarks(existingRemarks.Concat(newSegments.Select(segment => ImportDaySplitPlanner.DefaultRemark(segment.Start))))
                .Skip(existingRemarks.Length)
                .ToArray();
            var remarkByIndex = newSegments.Select((segment, i) => (segment.Index, Remark: remarks[i])).ToDictionary(pair => pair.Index, pair => pair.Remark);

            foreach (var segment in day.Segments)
            {
                var decision = new ImportDateFolderDecision(day.Date, segment.Count, candidates, Refresh);
                decision.ApplySegment(segment, day.Segments.Count, day.AutoSegmentCount, false, segment.Keys);
                if (remarkByIndex.TryGetValue(segment.Index, out var remark))
                {
                    decision.PresetStrategy(ImportDateFolderStrategy.CreateSeparate, remark);
                }
                Refresh(decision);
                decisions.Add(decision);
            }
        }
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
