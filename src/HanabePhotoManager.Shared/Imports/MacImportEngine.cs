using System.Globalization;
using System.Text.RegularExpressions;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Mac.Services;

/// <summary>一组待导入素材（主文件 + 同名伴随文件），带解析出的拍摄日期。</summary>
public sealed class ImportGroupItem(MediaGroup group, LibraryDate? date)
{
    public MediaGroup Group { get; } = group;
    public LibraryDate? Date { get; } = date;
    public bool IsSelected { get; set; } = true;
    public string CategoryName => MacImportEngine.CategoryName(Group.Category);
    public long TotalBytes => Group.Primary.Length + Group.Sidecars.Sum(file => file.Length);
    public bool NeedsAttention => Date is null || !MacImportEngine.IsConcrete(Group.Category);
}

public sealed record ImportRunResult(int Success, int Skipped, int Failed, IReadOnlyList<string> Lines);

/// <summary>
/// macOS 导入引擎：规则与 Windows 版一致——同样的扩展名、分类、日期判定（相机文件夹名里的月日，否则文件修改日期）、
/// 同样的日期文件夹决策、导入计划和“先写临时文件 → SHA-256 校验 → 原子改名”的复制。
/// 「修后」目录永不写入由导入计划保证（只写入目标日期文件夹的分类子目录）。
/// </summary>
public sealed class MacImportEngine
{
    public static readonly HashSet<string> RawExtensions = new(
        [".arw", ".cr2", ".cr3", ".nef", ".raf", ".rw2", ".orf", ".dng", ".raw"], StringComparer.OrdinalIgnoreCase);

    public static readonly HashSet<string> VideoExtensions = new(
        [".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v", ".mxf", ".mts", ".m2ts", ".ts",
         ".webm", ".mpeg", ".mpg", ".3gp", ".flv", ".ogv", ".m2t", ".mod"], StringComparer.OrdinalIgnoreCase);

    public static readonly HashSet<string> ImportExtensions = new(
        RawExtensions.Concat(VideoExtensions).Concat(
            [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".heif", ".hif"]),
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<MediaCategory, string> CategoryFolders = new Dictionary<MediaCategory, string>
    {
        [MediaCategory.Raw] = "RAW生图",
        [MediaCategory.Jpeg] = "JPG生图",
        [MediaCategory.Edited] = "修后",
        [MediaCategory.Video] = "视频",
        [MediaCategory.ActionVideo] = "action视频",
        [MediaCategory.Material] = "素材",
    };

    /// <summary>扫描时跳过的系统目录与 macOS 资源分叉文件（._xxx）。</summary>
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
        { ".Trashes", ".Spotlight-V100", ".fseventsd", ".TemporaryItems", "System Volume Information", "$RECYCLE.BIN" };

    private readonly Sha256FileHasher _hasher = new();
    private readonly ImportDateFolderPreflightService _preflight = new();
    private readonly LibraryContentScanner _scanner;
    private readonly ImportPlanBuilder _planBuilder;
    private readonly VerifiedFileTransfer _transfer;

    public MacImportEngine()
    {
        _scanner = new LibraryContentScanner(_hasher);
        _planBuilder = new ImportPlanBuilder(new DestinationProbe(_hasher));
        _transfer = new VerifiedFileTransfer(_hasher);
    }

    public static string CategoryName(MediaCategory category) =>
        CategoryFolders.TryGetValue(category, out var name) ? name : "待确认";

    public static bool IsConcrete(MediaCategory category) => CategoryFolders.ContainsKey(category);

    // ---------- 1 分析（只读） ----------

    public async Task<IReadOnlyList<ImportGroupItem>> AnalyzeAsync(IReadOnlyList<string> roots, IProgress<string>? progress, CancellationToken token)
    {
        var files = await Task.Run(() => Scan(roots, progress, token), token).ConfigureAwait(false);
        progress?.Report($"正在分类 {files.Count:N0} 个文件…");
        // 与 Windows 版相同：RAW 扩展名由调用方传入；视频容器扩展名 MediaClassifier 已内置，第二个参数只放自定义扩展名。
        var groups = new MediaGroupBuilder(new MediaClassifier(RawExtensions, Array.Empty<string>())).Build(files);
        return groups.Select(group => new ImportGroupItem(group, ResolveFileDate(group.Primary))).ToArray();
    }

    private static List<SourceMediaFile> Scan(IReadOnlyList<string> roots, IProgress<string>? progress, CancellationToken token)
    {
        var files = new List<SourceMediaFile>();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var pending = new Stack<string>(roots.Where(Directory.Exists));
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            try
            {
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    var name = Path.GetFileName(child);
                    if (!SkippedDirectories.Contains(name) && !name.StartsWith('.')) pending.Push(child);
                }
                foreach (var path in Directory.EnumerateFiles(directory))
                {
                    var name = Path.GetFileName(path);
                    if (name.StartsWith("._", StringComparison.Ordinal) || !ImportExtensions.Contains(Path.GetExtension(path))) continue;
                    if (!seen.Add(path)) continue;
                    var info = new FileInfo(path);
                    files.Add(new SourceMediaFile(info.FullName, info.Length, info.LastWriteTimeUtc));
                    if (files.Count % 200 == 0) progress?.Report($"正在读取来源… 已发现 {files.Count:N0} 个文件");
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        return files;
    }

    /// <summary>与 Windows 版相同：相机文件夹名末尾 4 位数字视为 MMDD，否则用文件修改日期（本地时间）。</summary>
    public static LibraryDate? ResolveFileDate(SourceMediaFile file)
    {
        var directory = Path.GetDirectoryName(file.FullPath);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var match = Regex.Matches(Path.GetFileName(directory) ?? string.Empty, "[0-9]{4,}").LastOrDefault();
            if (match is not null)
            {
                var monthDay = match.Value[^4..];
                if (int.TryParse(monthDay[..2], CultureInfo.InvariantCulture, out var month) &&
                    int.TryParse(monthDay[2..], CultureInfo.InvariantCulture, out var day) &&
                    month is >= 1 and <= 12 && day is >= 1 and <= 31)
                {
                    try { return new LibraryDate(file.LastWriteTime.Year, month, day); }
                    catch (ArgumentOutOfRangeException) { }
                }
            }
            directory = Path.GetDirectoryName(directory);
        }

        var local = file.LastWriteTime.LocalDateTime;
        return new LibraryDate(local.Year, local.Month, local.Day);
    }

    // ---------- 2 日期方案 + 查重 ----------

    public IReadOnlyList<ImportDateFolderDecision> CreateDecisions(string libraryRoot, IEnumerable<ImportGroupItem> items) =>
        CreateDecisions(libraryRoot, items, autoSplit: false, TimeSpan.FromHours(2));

    /// <summary>
    /// 与 Windows 版相同的日期方案；autoSplit 时同一天隔了很久会分成几个文件夹（HANABE-IMPORT-DAY-SPLIT），
    /// keptTogether / mergedAnchors 记录用户“都放进一个文件夹 / 这两段放一起”的选择。
    /// 拍摄时间用文件修改时间（相机写卡时间）。
    /// </summary>
    public IReadOnlyList<ImportDateFolderDecision> CreateDecisions(
        string libraryRoot,
        IEnumerable<ImportGroupItem> items,
        bool autoSplit,
        TimeSpan threshold,
        IReadOnlySet<LibraryDate>? keptTogether = null,
        IReadOnlySet<string>? mergedAnchors = null)
    {
        var plans = ImportDayPlanBuilder.Build(
            items.Where(item => item.IsSelected && item.Date is not null && IsConcrete(item.Group.Category))
                .Select(item => new ImportDayItem(item.Date!.Value, item.Group.Primary.FullPath,
                    item.Group.Primary.LastWriteTime.LocalDateTime, ImportDayPlanBuilder.PairKeyFor(item.Group.Primary.FullPath))),
            autoSplit, threshold, keptTogether, mergedAnchors);
        return _preflight.CreateSegmentedDecisions(libraryRoot, plans);
    }

    /// <summary>这个决定负责哪些导入项：分段时按成员，整天时按日期。</summary>
    public static bool Covers(ImportDateFolderDecision decision, ImportGroupItem item) =>
        item.Date == decision.Date &&
        (decision.MemberKeys.Count == 0 || decision.MemberKeys.Contains(item.Group.Primary.FullPath, StringComparer.OrdinalIgnoreCase));

    /// <summary>与同一天的所有日期文件夹比对（尺寸 → 头尾指纹 → SHA-256），返回重复的主文件路径。</summary>
    public async Task<HashSet<string>> DetectDuplicatesAsync(string libraryRoot, IReadOnlyList<ImportGroupItem> items, CancellationToken token)
    {
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(libraryRoot) || !Directory.Exists(libraryRoot)) return duplicates;
        var entries = LibraryDateFolderService.Scan(Path.GetFullPath(libraryRoot));
        foreach (var dateGroup in items.Where(item => item.IsSelected && item.Date is not null).GroupBy(item => item.Date!.Value))
        {
            token.ThrowIfCancellationRequested();
            var folders = entries.Where(entry => entry.Month == dateGroup.Key.Month && entry.Day == dateGroup.Key.Day).Select(entry => entry.FullPath).ToArray();
            if (folders.Length == 0) continue;
            var merged = new Dictionary<long, List<string>>();
            foreach (var folder in folders)
            {
                var map = await _scanner.BuildSizeMapAsync(folder, ImportExtensions, token).ConfigureAwait(false);
                foreach (var (size, paths) in map)
                {
                    if (!merged.TryGetValue(size, out var list)) merged[size] = list = [];
                    list.AddRange(paths);
                }
            }
            foreach (var item in dateGroup)
            {
                if (await _scanner.FindContentDuplicateAsync(item.Group.Primary.FullPath, merged, token).ConfigureAwait(false) is not null)
                    duplicates.Add(item.Group.Primary.FullPath);
            }
        }
        return duplicates;
    }

    // ---------- 3 导入 ----------

    public async Task<ImportRunResult> RunAsync(
        string libraryRoot,
        IReadOnlyList<ImportDateFolderDecision> decisions,
        IReadOnlyList<ImportGroupItem> items,
        bool moveAfterVerify,
        IReadOnlySet<string> skipPaths,
        IProgress<(int Done, int Total, string Name)>? progress,
        CancellationToken token)
    {
        var lines = new List<string>();
        int success = 0, skipped = 0, failed = 0;
        foreach (var decision in decisions) _preflight.Refresh(decision);
        if (decisions.Any(decision => !decision.IsValid))
            throw new InvalidOperationException("有日期的目标文件夹还需要调整：" + string.Join("；", decisions.Where(d => !d.IsValid).Select(d => $"{d.DateText} {d.ValidationMessage}")));
        if (!_preflight.ApplyRequiredRenames(decisions))
            throw new IOException("日期文件夹改名失败，没有复制任何文件。");

        var selected = items.Where(item => item.IsSelected && item.Date is not null && IsConcrete(item.Group.Category)).ToArray();
        var total = selected.Length;
        var done = 0;
        var mode = moveAfterVerify ? TransferMode.MoveAfterVerify : TransferMode.CopyKeepSource;
        foreach (var decision in decisions)
        {
            var groups = selected.Where(item => Covers(decision, item)).ToArray();
            var toImport = groups.Where(item => !skipPaths.Contains(item.Group.Primary.FullPath)).Select(item => item.Group).ToArray();
            foreach (var duplicate in groups.Where(item => skipPaths.Contains(item.Group.Primary.FullPath)))
            {
                skipped++;
                done++;
                lines.Add($"重复，跳过：{decision.DateText} / {duplicate.Group.GroupKey}");
            }
            if (toImport.Length == 0) continue;

            var plan = await _planBuilder.BuildAsync(libraryRoot, decision.Date, mode, toImport, token, null, decision.FinalDirectoryPath).ConfigureAwait(false);
            foreach (var planned in plan.Items)
            {
                token.ThrowIfCancellationRequested();
                var item = planned.Conflict == ConflictKind.SameNameDifferentContent ? ImportConflictNameResolver.Resolve(planned) : planned;
                progress?.Report((done, total, item.Group.GroupKey));
                var result = await _transfer.TransferGroupAsync(item, moveAfterVerify, token).ConfigureAwait(false);
                done++;
                if (!result.Success)
                {
                    failed++;
                    lines.Add($"失败：{decision.DateText} / {item.Group.GroupKey} · {result.Error}");
                }
                else if (item.Conflict == ConflictKind.Identical)
                {
                    skipped++;
                    lines.Add($"已存在相同文件，跳过：{decision.DateText} / {item.Group.GroupKey}");
                }
                else
                {
                    success++;
                    lines.Add($"完成：{decision.DateText} / {item.Group.GroupKey}");
                }
            }
        }

        progress?.Report((done, total, string.Empty));
        lines.Insert(0, $"导入完成：成功 {success}，跳过 {skipped}，失败 {failed}");
        return new ImportRunResult(success, skipped, failed, lines);
    }
}
