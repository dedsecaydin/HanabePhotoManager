using System.Collections.Concurrent;
using System.IO;
using HanabePhotoManager.App.Duplicates;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>
/// 导入查重：
/// 1. 范围 = 目标日期文件夹 + 同一天的其它备注文件夹（避免“新建另一个备注文件夹”时漏掉已导入过的照片）；
/// 2. 先比尺寸，再比头尾快速指纹，最后 SHA-256 确认；多个来源并行检查；
/// 3. 进入第 2 步“确认日期”时就在后台预查，让用户在开始导入前看到每个日期有多少重复。
/// </summary>
public sealed partial class MainWindowViewModel
{
    private CancellationTokenSource? _importDuplicatePreviewCancellation;
    private string _importDuplicatePreviewText = string.Empty;

    /// <summary>第 2 步顶部的查重摘要；空字符串时不显示。</summary>
    public string ImportDuplicatePreviewText
    {
        get => _importDuplicatePreviewText;
        private set
        {
            if (SetProperty(ref _importDuplicatePreviewText, value ?? string.Empty))
                OnPropertyChanged(nameof(HasImportDuplicatePreview));
        }
    }

    public bool HasImportDuplicatePreview => ImportDuplicatePreviewText.Length > 0;

    private string ImportDuplicateHandlingVerb => _importDuplicateHandling switch
    {
        ImportDuplicateBatchDecision.ImportAll => "仍然导入",
        ImportDuplicateBatchDecision.DecideIndividually => "逐个询问",
        _ => "跳过",
    };

    /// <summary>
    /// 并行检查每个来源是否已在其日期范围内存在（内容相同 / 来源元数据相同），再检查本批来源之间的重复。
    /// 返回“来源路径 → 匹配”的字典；不修改任何文件。
    /// </summary>
    private async Task<Dictionary<string, ImportDuplicateMatch>> DetectImportDuplicatesAsync(
        IReadOnlyList<ImportPreviewItemViewModel> items,
        Func<ImportPreviewItemViewModel, CancellationToken, Task<IReadOnlyDictionary<long, List<string>>?>>? dateSizeMapResolver,
        IProgress<(int Done, int Total, string Name)>? progress,
        CancellationToken cancellationToken,
        OriginMetadataDuplicateMatcher? matcher = null)
    {
        if (matcher is null)
        {
            matcher = _originDuplicateMatcher;
            matcher.BeginBatch();
        }

        var libraryRoot = LibraryRoot;
        var matches = new ConcurrentDictionary<string, ImportDuplicateMatch>(StringComparer.OrdinalIgnoreCase);
        var itemByPath = items
            .GroupBy(item => item.ToMediaGroup().Primary.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var sourcePaths = itemByPath.Keys.ToArray();

        if (dateSizeMapResolver is not null)
        {
            // 先按日期解析范围（解析器自带按日期缓存），再并行比对，避免并行时重复枚举文件夹。
            var work = new List<(string Path, IReadOnlyDictionary<long, List<string>> Map, string[] OriginCandidates)>();
            var originCandidates = new Dictionary<IReadOnlyDictionary<long, List<string>>, string[]>(ReferenceEqualityComparer.Instance);
            foreach (var sourcePath in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = itemByPath[sourcePath];
                // 无目标日期：跳过目标文件夹比对（只保留下方源内查重）。
                if (item.TargetDate is null) continue;
                var map = await dateSizeMapResolver(item, cancellationToken).ConfigureAwait(true);
                if (map is null || map.Count == 0) continue;
                if (!originCandidates.TryGetValue(map, out var candidates))
                    originCandidates[map] = candidates = map.Values.SelectMany(paths => paths).ToArray();
                work.Add((sourcePath, map, candidates));
            }

            var done = 0;
            await Parallel.ForEachAsync(work, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(DuplicateHashParallelism, 1, 4),
            }, async (entry, token) =>
            {
                var existing = await _contentScanner.FindContentDuplicateAsync(entry.Path, entry.Map, token).ConfigureAwait(false)
                    ?? await matcher.FindMatchAsync(entry.Path, entry.OriginCandidates, token).ConfigureAwait(false);
                if (existing is not null)
                {
                    matches[entry.Path] = new ImportDuplicateMatch(entry.Path, existing,
                        RetouchedDirectoryPolicy.IsReadOnlyRetouchedPath(libraryRoot, existing));
                }
                progress?.Report((Interlocked.Increment(ref done), work.Count, Path.GetFileName(entry.Path)));
            }).ConfigureAwait(false);
        }

        // 本批来源之间的重复（例如同一张卡被选了两次、或两个文件夹里有同一份文件）：
        // 尺寸 → 快速指纹 → SHA-256，按路径顺序保留第一份。
        var sameSizeGroups = sourcePaths
            .Where(path => !matches.ContainsKey(path))
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists)
            .GroupBy(info => info.Length)
            .Where(group => group.Count() >= 2);
        foreach (var sameSize in sameSizeGroups)
        {
            var byQuick = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sameSize.OrderBy(info => info.FullName, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var quick = await _contentScanner.ComputeQuickFingerprintAsync(source.FullName, cancellationToken).ConfigureAwait(false);
                if (!byQuick.TryGetValue(quick, out var list)) byQuick[quick] = list = [];
                list.Add(source.FullName);
            }

            foreach (var candidates in byQuick.Values.Where(list => list.Count >= 2))
            {
                var firstPathByHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var hash = await _duplicateHasher.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
                    if (firstPathByHash.TryGetValue(hash, out var firstPath))
                        matches.TryAdd(path, new ImportDuplicateMatch(path, firstPath, false));
                    else
                        firstPathByHash.Add(hash, path);
                }
            }
        }

        return new Dictionary<string, ImportDuplicateMatch>(matches, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>图库中与 <paramref name="date"/> 同月同日的全部日期文件夹（含不同备注）。</summary>
    private static IReadOnlyList<string> FindSameDayFolders(IReadOnlyList<DateFolderEntry> entries, LibraryDate date) =>
        entries.Where(entry => entry.Month == date.Month && entry.Day == date.Day).Select(entry => entry.FullPath).ToArray();

    private static IReadOnlyList<DateFolderEntry> ScanDateFoldersSafe(string libraryRoot)
    {
        try { return string.IsNullOrWhiteSpace(libraryRoot) ? [] : LibraryDateFolderService.Scan(Path.GetFullPath(libraryRoot)); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    /// <summary>把多个文件夹的“大小 → 路径”映射合并为一个查重范围；重复路径只保留一次。</summary>
    private async Task<IReadOnlyDictionary<long, List<string>>> BuildDuplicateScopeAsync(
        IEnumerable<string> directories, CancellationToken token)
    {
        var merged = new Dictionary<long, List<string>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            var map = await Task.Run(() => _contentScanner.BuildSizeMapAsync(directory, ContentScanExtensions, token), token)
                .ConfigureAwait(true);
            foreach (var (size, paths) in map)
            {
                foreach (var path in paths)
                {
                    if (!seen.Add(path)) continue;
                    if (!merged.TryGetValue(size, out var list)) merged[size] = list = [];
                    list.Add(path);
                }
            }
        }
        return merged;
    }

    /// <summary>为导入 / 续传构建“按日期缓存”的查重范围解析器：目标文件夹 + 同日其它文件夹。</summary>
    private Func<ImportPreviewItemViewModel, CancellationToken, Task<IReadOnlyDictionary<long, List<string>>?>> CreateDateScopeResolver(
        Func<ImportPreviewItemViewModel, string?> targetDirectory)
    {
        var entries = new Lazy<IReadOnlyList<DateFolderEntry>>(() => ScanDateFoldersSafe(LibraryRoot));
        var cache = new Dictionary<string, IReadOnlyDictionary<long, List<string>>>(StringComparer.OrdinalIgnoreCase);
        return async (item, token) =>
        {
            if (item.TargetDate is not { } date) return null;
            var primary = targetDirectory(item);
            var key = $"{date.Month}.{date.Day}|{primary}";
            if (!cache.TryGetValue(key, out var map))
            {
                var directories = new List<string>();
                if (!string.IsNullOrWhiteSpace(primary)) directories.Add(primary);
                directories.AddRange(FindSameDayFolders(entries.Value, date));
                map = await BuildDuplicateScopeAsync(directories, token).ConfigureAwait(true);
                cache[key] = map;
            }
            return map;
        };
    }

    /// <summary>进入第 2 步时在后台预查重复；不占用“忙碌”状态，确认按钮随时可点。</summary>
    private async void StartImportDuplicatePreview(IReadOnlyList<ImportPreviewItemViewModel> items)
    {
        CancelImportDuplicatePreview();
        foreach (var decision in ImportDateFolderDecisions) decision.DuplicateSummary = string.Empty;
        if (!CheckDuplicatesOnImport)
        {
            ImportDuplicatePreviewText = "精确查重已关闭：内容相同的文件也会再次导入。";
            return;
        }
        if (items.Count == 0) { ImportDuplicatePreviewText = string.Empty; return; }

        var cancellation = new CancellationTokenSource();
        _importDuplicatePreviewCancellation = cancellation;
        var token = cancellation.Token;
        ImportDuplicatePreviewText = "正在后台查重…不影响确认，可以直接开始导入。";
        foreach (var decision in ImportDateFolderDecisions) decision.DuplicateSummary = "查重中…";
        try
        {
            var progress = new Progress<(int Done, int Total, string Name)>(update =>
            {
                if (!token.IsCancellationRequested)
                    ImportDuplicatePreviewText = $"正在后台查重 {update.Done}/{update.Total}…不影响确认，可以直接开始导入。";
            });
            // 预查时最终文件夹还未确定：同日全部已有文件夹已覆盖“使用原文件夹 / 改备注”两种情况。
            var resolver = CreateDateScopeResolver(_ => null);
            var matches = await DetectImportDuplicatesAsync(items, resolver, progress, token,
                new OriginMetadataDuplicateMatcher(_originMetadataStore, _duplicateHasher)).ConfigureAwait(true);
            if (token.IsCancellationRequested) return;

            var datesByPath = items
                .Where(item => item.TargetDate is not null)
                .GroupBy(item => item.PrimaryPath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().TargetDate!.Value, StringComparer.OrdinalIgnoreCase);
            _importDuplicatePreviewCounts = matches.Keys
                .Where(datesByPath.ContainsKey)
                .GroupBy(path => datesByPath[path])
                .ToDictionary(group => group.Key, group => group.Count());
            _importDuplicatePreviewPaths = new HashSet<string>(matches.Keys, StringComparer.OrdinalIgnoreCase);
            _importDuplicatePreviewTotal = matches.Count;
            ApplyImportDuplicatePreviewTexts();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested) return;
            foreach (var decision in ImportDateFolderDecisions) decision.DuplicateSummary = string.Empty;
            ImportDuplicatePreviewText = "后台查重未完成：" + ex.Message + "。开始导入时会重新检查。";
        }
        finally
        {
            if (ReferenceEquals(_importDuplicatePreviewCancellation, cancellation)) _importDuplicatePreviewCancellation = null;
            cancellation.Dispose();
        }
    }

    private Dictionary<LibraryDate, int>? _importDuplicatePreviewCounts;
    private HashSet<string>? _importDuplicatePreviewPaths;
    private int _importDuplicatePreviewTotal;

    /// <summary>按当前“重复文件处理”选项刷新第 2 步的查重文字（切换选项时无需重新计算）。</summary>
    private void ApplyImportDuplicatePreviewTexts()
    {
        if (_importDuplicatePreviewCounts is not { } counts) return;
        foreach (var decision in ImportDateFolderDecisions)
        {
            // 分段后的决定只统计本段成员；整天决定沿用按日期的计数。
            var count = decision.MemberKeys.Count > 0 && _importDuplicatePreviewPaths is { } paths
                ? decision.MemberKeys.Count(paths.Contains)
                : counts.GetValueOrDefault(decision.Date);
            decision.DuplicateSummary = count > 0
                ? $"{count} 个重复 · 将{ImportDuplicateHandlingVerb}"
                : "无重复";
        }
        OnPropertyChanged(nameof(ImportReviewSummary));
        OnPropertyChanged(nameof(ImportDuplicateCountText));
        ImportDuplicatePreviewText = _importDuplicatePreviewTotal == 0
            ? "查重完成：没有发现与图库已有文件或本批其它文件重复的内容。"
            : $"查重完成：{_importDuplicatePreviewTotal} 个文件与该日期已有文件或本批其它文件内容完全相同，开始导入后将{ImportDuplicateHandlingVerb}（可在右上角「导入设置」修改）。";
    }

    /// <summary>查重开关或处理方式改变时，让第 2 步的预查结果保持一致。</summary>
    private void OnImportDuplicateOptionsChanged(bool checkSettingChanged)
    {
        if (!IsImportDateFolderPreflightVisible) return;
        if (checkSettingChanged) StartImportDuplicatePreview(_preparedImportItems.Select(row => row.Item).ToArray());
        else ApplyImportDuplicatePreviewTexts();
    }

    private void CancelImportDuplicatePreview()
    {
        _importDuplicatePreviewCounts = null;
        var cancellation = _importDuplicatePreviewCancellation;
        _importDuplicatePreviewCancellation = null;
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
    }
}
