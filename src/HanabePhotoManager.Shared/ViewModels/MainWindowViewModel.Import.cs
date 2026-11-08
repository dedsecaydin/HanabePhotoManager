using HanabePhotoManager.App.Duplicates;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;
using System.IO;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.Platform;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>主窗口中与多文件夹来源选择、拖放验证和导入队列初始化相关的职责。</summary>
public sealed partial class MainWindowViewModel
{
    internal Func<string, CancellationToken, SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>> HeifDecoder { get; set; } = (path, token) => PlatformServices.DecodeHeif(path, token);
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

    private async Task BrowseSourceFoldersAsync()
    {
        var selectedPaths = _importSourcePicker.PickFolders(SourceFolder)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selectedPaths.Length == 0)
        {
            return;
        }

        ClearImportDateFolderPreflight();
        CancelImportThumbnailLoading();
        ImportItems.Clear();
        ImportSections.Clear();
        SetImportSummary(0, 0, 0);
        TargetDateText = "等待分析日期";
        StatusMessage = $"已选择 {selectedPaths.Length} 个来源文件夹，正在分析。";
        ProgressValue = 0;
        ProgressLabel = "等待开始";
        // 选好即分析（只读），统一检查日期与目标，省掉一次“分析并预览导入”点击。
        await PrepareMultipleImportSourcesAsync(selectedPaths).ConfigureAwait(true);
        NotifyCommandStates();
    }

    private async Task<(IReadOnlyDictionary<string, ImportDuplicateMatch> Matches, ImportDuplicateBatchDecision Decision)>
        PrepareDuplicateBatchAsync(
            IReadOnlyList<ImportPreviewItemViewModel> items,
            Func<ImportPreviewItemViewModel, CancellationToken, Task<IReadOnlyDictionary<long, List<string>>?>>? dateSizeMapResolver,
            CancellationToken cancellationToken)
    {
        var progress = new Progress<(int Done, int Total, string Name)>(update =>
        {
            if (cancellationToken.IsCancellationRequested) return;
            ProgressLabel = $"正在查重 {update.Done}/{update.Total}：{update.Name}";
            ProgressValue = update.Done * 100d / Math.Max(update.Total, 1);
        });
        var matches = await DetectImportDuplicatesAsync(items, dateSizeMapResolver, progress, cancellationToken)
            .ConfigureAwait(true);

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
        return await new HeifJpegConverter(HeifDecoder).EnsureJpegAsync(original, token, reservedPaths).ConfigureAwait(true);
    }
}
