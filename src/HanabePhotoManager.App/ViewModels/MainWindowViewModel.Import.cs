using HanabePhotoManager.App.Duplicates;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;
using System.IO;
using System.Windows;

namespace HanabePhotoManager.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private Task BrowseSourceFilesAsync()
    {
        var selectedPaths = _importSourcePicker.PickFiles(SourceFolder)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selectedPaths.Length == 0)
        {
            return Task.CompletedTask;
        }

        SourceFolder = ResolveDroppedSourceDisplayPath(selectedPaths);
        _sourceScanPaths = selectedPaths;
        CancelImportThumbnailLoading();
        ImportItems.Clear();
        ImportSections.Clear();
        SetImportSummary(0, 0, 0);
        TargetDateText = "等待分析日期";
        ImportReport = $"已选择 {selectedPaths.Length} 个文件，尚未开始分析。";
        ImportActionHint = "已保留 Ctrl/Shift 多选文件；点击“开始分析与导入”后加入同一导入队列。";
        StatusMessage = $"已选择 {selectedPaths.Length} 个来源文件。";
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
        var matches = new Dictionary<string, ImportDuplicateMatch>(StringComparer.OrdinalIgnoreCase);
        var sourcePaths = items
            .Select(item => item.ToMediaGroup().Primary.FullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (dateSizeMapResolver is not null)
        {
            var itemByPath = items
                .GroupBy(item => item.ToMediaGroup().Primary.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var sourcePath in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
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

                var existingPath = await _contentScanner.FindContentDuplicateAsync(sourcePath, dateSizeMap, cancellationToken)
                    .ConfigureAwait(true);
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
                var hash = await _fileHasher.ComputeSha256Async(source.FullName, cancellationToken).ConfigureAwait(true);
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

        var window = new ImportDuplicateBatchDecisionWindow(matches.Values.ToArray())
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        return window.ShowDialog() == true
            ? (matches, window.Decision)
            : (matches, ImportDuplicateBatchDecision.SkipAll);
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
}
