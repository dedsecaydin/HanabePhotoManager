using HanabePhotoManager.App.Duplicates;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;
using System.IO;
using System.Windows;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>主窗口中与多文件夹来源选择、拖放验证和导入队列初始化相关的职责。</summary>
public sealed partial class MainWindowViewModel
{
    private bool _importAnalysisSucceeded;
    public string ImportPrimaryActionText => HasPendingImportResume ? "继续未完成导入"
        : ImportItems.Count > 0 ? "下一步：确认日期与目标" : "分析并预览导入";

    public IReadOnlyList<string> SelectedImportSourceFolders => _sourceScanPaths;

    private void SetImportSourcePaths(IReadOnlyList<string> paths)
    {
        ClearImportDateFolderPreflight();
        _importAnalysisSucceeded = false;
        _sourceScanPaths = paths;
        OnPropertyChanged(nameof(SelectedImportSourceFolders));
    }

    private Task BrowseSourceFoldersAsync()
    {
        var selectedPaths = _importSourcePicker.PickFolders(SourceFolder)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selectedPaths.Length == 0)
        {
            return Task.CompletedTask;
        }

        SourceFolder = ResolveDroppedSourceDisplayPath(selectedPaths);
        SetImportSourcePaths(selectedPaths);
        ClearImportDateFolderPreflight();
        CancelImportThumbnailLoading();
        ImportItems.Clear();
        ImportSections.Clear();
        SetImportSummary(0, 0, 0);
        TargetDateText = "等待分析日期";
        ImportReport = $"已选择 {selectedPaths.Length} 个来源文件夹，尚未开始分析。";
        ImportActionHint = "支持 Ctrl/Shift 多选文件夹；点击“分析并预览导入”后统一检查日期与目标。";
        StatusMessage = $"已选择 {selectedPaths.Length} 个来源文件夹。";
        ProgressValue = 0;
        ProgressLabel = "等待开始";
        NotifyCommandStates();
        return Task.CompletedTask;
    }

    private async Task<(IReadOnlyDictionary<string, ImportDuplicateMatch> Matches, ImportDuplicateBatchDecision Decision)>
        PrepareDuplicateBatchAsync(
            IReadOnlyList<ImportPreviewItemViewModel> items,
            Func<ImportPreviewItemViewModel, CancellationToken, Task<IReadOnlyDictionary<long, List<string>>?>>? dateSizeMapResolver,
            CancellationToken cancellationToken)
    {
        _originDuplicateMatcher.BeginBatch();
        var matches = new Dictionary<string, ImportDuplicateMatch>(StringComparer.OrdinalIgnoreCase);
        var originCandidates = new Dictionary<IReadOnlyDictionary<long, List<string>>, string[]>(ReferenceEqualityComparer.Instance);
        var sourcePaths = items
            .Select(item => item.ToMediaGroup().Primary.FullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (dateSizeMapResolver is not null)
        {
            var itemByPath = items
                .GroupBy(item => item.ToMediaGroup().Primary.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var checkedSources = 0;
            foreach (var sourcePath in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                checkedSources++;
                if (!itemByPath.TryGetValue(sourcePath, out var item) || item.TargetDate is null)
                {
                    // 无目标日期：跳过目标文件夹比对（只保留下方源内查重）。
                    continue;
                }

                var dateSizeMap = await dateSizeMapResolver(item, cancellationToken).ConfigureAwait(true);
                if (dateSizeMap is null)
                {
                    continue;
                }

                ProgressLabel = $"正在查重 {checkedSources}/{sourcePaths.Length}：{Path.GetFileName(sourcePath)}";
                ProgressValue = checkedSources * 100d / Math.Max(sourcePaths.Length, 1);
                var existingPath = await _contentScanner.FindContentDuplicateAsync(sourcePath, dateSizeMap, cancellationToken)
                    .ConfigureAwait(true);
                if (existingPath is null)
                {
                    if (!originCandidates.TryGetValue(dateSizeMap, out var dateCandidates))
                        originCandidates[dateSizeMap] = dateCandidates = dateSizeMap.Values.SelectMany(paths => paths).ToArray();
                    existingPath = await _originDuplicateMatcher.FindMatchAsync(sourcePath, dateCandidates, cancellationToken).ConfigureAwait(true);
                }
                if (existingPath is not null)
                {
                    matches[sourcePath] = new ImportDuplicateMatch(
                        sourcePath,
                        existingPath,
                        RetouchedDirectoryPolicy.IsReadOnlyRetouchedPath(LibraryRoot, existingPath));
                }
            }
        }

        var sourcesBySize = sourcePaths
            .Where(path => !matches.ContainsKey(path))
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists)
            .GroupBy(info => info.Length);
        foreach (var sameSizeSources in sourcesBySize)
        {
            if (sameSizeSources.Count() < 2)
            {
                continue;
            }

            var firstPathByHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sameSizeSources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var hash = await _duplicateHasher.ComputeSha256Async(source.FullName, cancellationToken).ConfigureAwait(true);
                if (firstPathByHash.TryGetValue(hash, out var firstPath))
                {
                    matches.TryAdd(source.FullName, new ImportDuplicateMatch(source.FullName, firstPath, false));
                }
                else
                {
                    firstPathByHash.Add(hash, source.FullName);
                }
            }
        }

        if (matches.Count == 0)
        {
            return (matches, ImportDuplicateBatchDecision.ImportAll);
        }

        // 导入页的选择就是本批决定，不能展示“全部跳过”却另外默认成仍导入。
        if (_importDuplicateHandling == ImportDuplicateBatchDecision.DecideIndividually)
            DuplicateActionRequired?.Invoke(this, matches.Count);
        return (matches, _importDuplicateHandling);
    }

    private bool ShouldTransferDuplicate(
        ImportDuplicateMatch match,
        ImportDuplicateBatchDecision batchDecision)
    {
        if (ImportDuplicateBatchDecisionPolicy.ShouldTransfer(batchDecision))
        {
            return true;
        }

        if (!ImportDuplicateBatchDecisionPolicy.ShouldPromptIndividually(batchDecision))
        {
            return false;
        }

        return ImportDuplicateDecisionPolicy.ShouldTransfer(
            ShowImportDuplicateDecision(match.IncomingPath, match.ExistingPath));
    }

    private async Task<string?> EnsureImportedHeifJpegAsync(ImportResumeEntry entry, CancellationToken token, IReadOnlySet<string> reservedPaths)
    {
        if (!HeifJpegConverter.IsHeif(entry.PrimaryPath)) return null;
        var original = entry.SkipTransfer ? entry.DuplicateExistingPath : entry.Plan?.Files.FirstOrDefault(file =>
            string.Equals(file.Source.FullPath, entry.PrimaryPath, StringComparison.OrdinalIgnoreCase))?.DestinationPath;
        original ??= entry.VerifiedFiles.FirstOrDefault(file =>
            string.Equals(file.File.Source.FullPath, entry.PrimaryPath, StringComparison.OrdinalIgnoreCase))?.File.DestinationPath;
        if (original is null)
            throw new IOException("HEIF 的已导入原件路径尚未确认，已保留记录；请连接原来源后继续查重。");
        // 源内重复由第一份已导入原件生成 JPG；绝不在来源卡上生成文件。
        if (original is null || !HeifJpegConverter.IsHeif(original)) return null;
        var relative = Path.GetRelativePath(Path.GetFullPath(LibraryRoot), Path.GetFullPath(original));
        if (Path.IsPathFullyQualified(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null;
        ProgressLabel = $"正在保留 HEIF 并生成 JPG：{Path.GetFileName(original)}";
        return await new HeifJpegConverter().EnsureJpegAsync(original, token, reservedPaths).ConfigureAwait(true);
    }
}
