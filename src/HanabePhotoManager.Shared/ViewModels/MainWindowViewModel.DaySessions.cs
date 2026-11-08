using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Browsing;
using HanabePhotoManager.App.Platform;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>
/// 已导入日期的同日分场（HANABE-LIBRARY-DAY-SESSIONS）：
/// 1) 同一天有多个文件夹（11.07 / 11.07_晚上）时，日期导航里归到同一个「11.07」父级下；
/// 2) 每个日期文件夹按拍摄间隔虚拟分场（规则与导入时的同日分段一致，开关和间隔也共用），
///    图库按场次分组，导航里显示场次子级，点一下跳到那一场；不动硬盘上的文件；
/// 3) 想真正拆开时「按场次拆成文件夹…」：预览确认后把第 2 场起的文件（含 RAW/JPG/视频/XML 等同名文件）
///    移到 11.07_晚上 这类新文件夹，「修后」不动，评分 / 标签跟着文件走，移动清单写入日志可追溯。
/// </summary>
public sealed partial class MainWindowViewModel
{
    private Dictionary<string, IReadOnlyList<LibraryDaySession>> _daySessions = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _daySessionLoadedFolders = new(StringComparer.OrdinalIgnoreCase);
    private (int Count, int Version, int Hours, bool Enabled, int Times) _daySessionStamp = (-1, -1, 0, false, -1);
    private LibraryCaptureTimeCache? _captureTimeCache;
    private int _captureTimeVersion;
    private readonly HashSet<string> _captureTimeQueued = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<(string Folder, PreviewFileViewModel[] Files)> _captureTimeQueue = new();
    private bool _captureTimeRunning;

    private LibraryCaptureTimeCache CaptureTimes => _captureTimeCache ??= new LibraryCaptureTimeCache();

    /// <summary>请求照片墙滚动到某个分组（场次）标题。</summary>
    public event Action<string>? GallerySectionScrollRequested;

    /// <summary>当前已加载的日期文件夹里，分成 2 场及以上的（键：日期文件夹完整路径）。</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<LibraryDaySession>> DaySessions => _daySessions;

    internal static string SessionSectionKey(string dateKey, int index) => $"{dateKey}#场次{index + 1}";

    private static bool IsSessionMedia(PreviewFileViewModel file) =>
        !string.Equals(file.Extension.TrimStart('.'), "xml", StringComparison.OrdinalIgnoreCase);

    private void EnsureDaySessions()
    {
        var stamp = (PreviewFiles.Count, _previewScanVersion, ImportDaySplitHours, AutoSplitImportDays, _captureTimeVersion);
        if (stamp == _daySessionStamp) return;
        _daySessionStamp = stamp;

        var sessions = new Dictionary<string, IReadOnlyList<LibraryDaySession>>(StringComparer.OrdinalIgnoreCase);
        var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in PreviewFiles
                     .Where(IsSessionMedia)
                     .GroupBy(file => ResolvePreviewDateSection(file.FullPath).Key, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Key == "other") continue;
            loaded.Add(group.Key);
            if (!AutoSplitImportDays || ImportDaySplitHours <= 0) continue;
            var files = group.ToArray();
            // 拍摄时间还没读完的文件夹先不分场（文件修改时间是导入时刻，拿来分会分错）
            if (CaptureTimesFor(files) is not { } times)
            {
                QueueCaptureTimeReading(group.Key, files);
                continue;
            }
            var plan = LibraryDaySessionPlanner.Plan(
                files.Select(file => (file.FullPath, file.Category, times.GetValueOrDefault(file.FullPath))),
                TimeSpan.FromHours(ImportDaySplitHours));
            if (plan.Count > 1) sessions[group.Key] = plan;
        }
        _daySessions = sessions;
        _daySessionLoadedFolders = loaded;
        UpdateNavigatorSessions();
    }

    /// <summary>
    /// 一个文件夹里每个文件的拍摄时间（同名的 RAW/JPG/视频/封面共用一个时间）。
    /// 还有没读过的照片 / 视频时返回 null。
    /// </summary>
    private Dictionary<string, DateTime?>? CaptureTimesFor(IReadOnlyList<PreviewFileViewModel> files)
    {
        var unitTimes = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        var unitRead = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var unit = LibraryDaySessionPlanner.StemKey(file.FullPath);
            if (!CaptureTimes.TryGet(file.FullPath, file.Length, file.CapturedAt, out var time)) continue;
            unitRead.Add(unit);
            if (time is not null && unitTimes.GetValueOrDefault(unit) is null) unitTimes[unit] = time;
        }
        var result = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var unit = LibraryDaySessionPlanner.StemKey(file.FullPath);
            if (!unitRead.Contains(unit) && !LibraryDaySessionPlanner.IsRetouched(file.Category)) return null;
            result[file.FullPath] = unitTimes.GetValueOrDefault(unit);
        }
        return result;
    }

    private void QueueCaptureTimeReading(string folder, PreviewFileViewModel[] files)
    {
        if (!_captureTimeQueued.Add(folder)) return;
        _captureTimeQueue.Enqueue((folder, files));
        if (_captureTimeRunning) return;
        _captureTimeRunning = true;
        _ = DrainCaptureTimeQueueAsync();
    }

    /// <summary>后台按文件夹读拍摄时间：每组同名文件只读一个（优先 JPG，再 RAW，再视频），4 个并发；读完一个文件夹就刷新分场。</summary>
    private async Task DrainCaptureTimeQueueAsync()
    {
        try
        {
            while (_captureTimeQueue.TryDequeue(out var job))
            {
                var cache = CaptureTimes;
                await Task.Run(() =>
                {
                    var units = job.Files
                        .Where(file => !LibraryDaySessionPlanner.IsRetouched(file.Category))
                        .GroupBy(file => LibraryDaySessionPlanner.StemKey(file.FullPath), StringComparer.OrdinalIgnoreCase)
                        .Where(unit => !unit.Any(file => cache.TryGet(file.FullPath, file.Length, file.CapturedAt, out _)))
                        .Select(unit => unit.OrderBy(ReadPriority).ToArray())
                        .ToArray();
                    Parallel.ForEach(units, new ParallelOptions { MaxDegreeOfParallelism = 4 }, unit =>
                    {
                        foreach (var file in unit)
                        {
                            var time = LibraryCaptureTimeCache.ReadCaptureTime(file.FullPath);
                            cache.Set(file.FullPath, file.Length, file.CapturedAt, time);
                            if (time is not null) break;
                        }
                    });
                    cache.Save();
                }).ConfigureAwait(false);

                PlatformServices.Dispatcher.Post(() =>
                {
                    _captureTimeQueued.Remove(job.Folder);
                    var before = SessionSignature();
                    _captureTimeVersion++;
                    EnsureDaySessions();
                    if (SessionSignature() != before && PreviewFiles.Count > 0) RefreshFilteredCache(resetPage: false);
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[DaySessions] capture time read failed: {ex.Message}");
        }
        finally
        {
            PlatformServices.Dispatcher.Post(() =>
            {
                _captureTimeRunning = false;
                if (_captureTimeQueue.Count > 0)
                {
                    _captureTimeRunning = true;
                    _ = DrainCaptureTimeQueueAsync();
                }
            });
        }
    }

    private static int ReadPriority(PreviewFileViewModel file) => file.Extension.TrimStart('.').ToUpperInvariant() switch
    {
        "JPG" or "JPEG" or "HEIC" or "HIF" => 0,
        "ARW" or "CR3" or "CR2" or "NEF" or "RAF" or "DNG" or "RW2" or "ORF" => 1,
        "MP4" or "MOV" or "MTS" or "M2TS" or "MXF" => 2,
        _ => 3,
    };

    private string SessionSignature() => string.Join("|", _daySessions
        .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
        .Select(pair => pair.Key + ":" + string.Join(",", pair.Value.Select(session => session.Count))));

    /// <summary>用指定间隔给一个已加载的日期文件夹分场（拆分窗口拖滑块时调用）。</summary>
    private IReadOnlyList<LibraryDaySession> PlanFolderSessions(string folder, double hours)
    {
        var files = PreviewFiles
            .Where(IsSessionMedia)
            .Where(file => string.Equals(ResolvePreviewDateSection(file.FullPath).Key, folder, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var times = CaptureTimesFor(files) ?? new Dictionary<string, DateTime?>();
        return LibraryDaySessionPlanner.Plan(
            files.Select(file => (file.FullPath, file.Category, times.GetValueOrDefault(file.FullPath))),
            TimeSpan.FromHours(Math.Max(0.25, hours)));
    }

    /// <summary>把一个日期分组按场次拆成多个照片墙分组；没有分场时原样加一个分组。</summary>
    private void AddPreviewDateSections(string dateKey, string title, PreviewFileViewModel[] items)
    {
        // 同一天有多个文件夹时（11.07 / 11.07_下午），标题带上文件夹备注，避免两个分组同名
        var folderName = Path.GetFileName(dateKey.TrimEnd('\\', '/'));
        if (folderName.Split('_', 2) is [_, var folderRemark] && !title.Contains(folderName, StringComparison.Ordinal))
            title = $"{title} · {folderRemark}";

        if (!_daySessions.TryGetValue(dateKey, out var sessions))
        {
            VisiblePreviewSections.Add(new PreviewDateSectionViewModel(
                dateKey, title, items,
                _personFilterOwnsExpansion || _previewDateExpansion.GetValueOrDefault(dateKey, true),
                OnPreviewDateSectionExpansionChanged,
                showHeader: true));
            return;
        }

        var sessionOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in sessions)
            foreach (var path in session.Paths)
                sessionOf.TryAdd(path, session.Index);

        // 场次顺序跟随当前排序（从新到旧时晚上的场在前）：按每场第一张在列表里出现的位置排
        var buckets = new List<(int Session, List<PreviewFileViewModel> Items)>();
        var bucketOf = new Dictionary<int, List<PreviewFileViewModel>>();
        foreach (var item in items)
        {
            var index = sessionOf.GetValueOrDefault(item.FullPath, 0);
            if (!bucketOf.TryGetValue(index, out var list))
            {
                list = [];
                bucketOf[index] = list;
                buckets.Add((index, list));
            }
            list.Add(item);
        }

        foreach (var (index, list) in buckets)
        {
            var session = sessions[index];
            var key = SessionSectionKey(dateKey, index);
            var subtitle = string.IsNullOrEmpty(session.GapText)
                ? $"第 {index + 1} 场 · {session.TimeText}"
                : $"第 {index + 1} 场 · {session.TimeText} · {session.GapText}";
            VisiblePreviewSections.Add(new PreviewDateSectionViewModel(
                key, $"{title} · {session.Remark}", list.ToArray(),
                _personFilterOwnsExpansion || _previewDateExpansion.GetValueOrDefault(key, true),
                OnPreviewDateSectionExpansionChanged,
                showHeader: true,
                subtitle: subtitle));
        }
    }

    private void UpdateNavigatorSessions()
    {
        foreach (var node in FlattenDateNodes(LibraryDates))
        {
            var key = node.FullPath.TrimEnd('\\', '/');
            if (_daySessions.TryGetValue(key, out var sessions))
                node.SetSessions(sessions.Select(session => new LibraryDateSessionNode(node, session, SessionSectionKey(key, session.Index))));
            else if (_daySessionLoadedFolders.Contains(key))
                node.SetSessions([]);
        }
    }

    private void OnDaySessionSettingsChanged()
    {
        _daySessionStamp = (-1, -1, 0, false, -1);
        if (PreviewFiles.Count > 0) RefreshFilteredCache(resetPage: false);
    }

    /// <summary>导航里点场次：展开并滚到那一场。</summary>
    [RelayCommand]
    private void ScrollToDaySession(LibraryDateSessionNode? session)
    {
        if (session is null) return;
        var section = VisiblePreviewSections.FirstOrDefault(candidate => candidate.Key == session.SectionKey);
        if (section is null)
        {
            StatusMessage = $"「{session.Title}」在当前筛选条件下没有照片。";
            return;
        }
        if (!section.IsExpanded) section.IsExpanded = true;
        GallerySectionScrollRequested?.Invoke(session.SectionKey);
    }

    /// <summary>按场次把一个日期文件夹真正拆成多个文件夹（先在窗口里调间隔、预览、确认）。</summary>
    [RelayCommand]
    private async Task SplitDayFolderBySessionsAsync(LibraryDateNode? node)
    {
        if (node is null || node.IsDayGroup || IsBusy) return;
        var folder = node.FullPath.TrimEnd('\\', '/');
        if (!_daySessions.TryGetValue(folder, out var sessions) || sessions.Count < 2) return;

        string[] files;
        try
        {
            files = await Task.Run(() => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToArray()).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取文件夹失败：{ex.Message}";
            return;
        }

        var folderName = Path.GetFileName(folder);
        var datePart = folderName.Split('_', 2)[0];
        static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);
        var plan = LibraryDaySessionPlanner.BuildSplitPlan(folder, sessions, files, Exists);
        var hours = (double)ImportDaySplitHours;

        if (PlatformServices.ConfirmDaySplit is { } confirmDaySplit)
        {
            var preview = new DaySplitPreview(folderName, hours, threshold =>
            {
                var planned = PlanFolderSessions(folder, threshold);
                if (planned.Count < 2)
                    return new DaySplitPreviewPlan(
                        [new DaySplitPreviewSession(0, planned.FirstOrDefault()?.Remark ?? string.Empty, planned.FirstOrDefault()?.TimeText ?? string.Empty, files.Length, string.Empty, datePart, folderName)],
                        0);
                var split = LibraryDaySessionPlanner.BuildSplitPlan(folder, planned, files, Exists);
                return new DaySplitPreviewPlan(
                    planned.Select((session, i) => new DaySplitPreviewSession(
                        i, session.Remark, session.TimeText, split.FilesPerFolder[i], session.GapText, datePart,
                        i == 0 ? folderName : null)).ToArray(),
                    split.Moves.Count);
            });
            if (!confirmDaySplit(preview)) return;
            // 按窗口里最终的间隔和备注重新生成计划
            sessions = PlanFolderSessions(folder, preview.ThresholdHours)
                .Select(session => session with
                {
                    Remark = session.Index < preview.Sessions.Count ? preview.Sessions[session.Index].Remark.Trim() : session.Remark,
                })
                .ToArray();
            if (sessions.Count < 2) return;
            plan = LibraryDaySessionPlanner.BuildSplitPlan(folder, sessions, files, Exists);
        }
        else if (!await PlatformServices.Dialogs.ConfirmAsync("按场次拆分到文件夹", BuildDaySplitMessage(folder, sessions, plan)).ConfigureAwait(true))
        {
            return;
        }
        if (plan.Moves.Count == 0)
        {
            StatusMessage = "没有需要移动的文件。";
            return;
        }

        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressLabel = "正在按场次拆分文件夹…";
        IReadOnlyList<LibraryFileMove> moved = [];
        var failures = new List<string>();
        try
        {
            (moved, failures) = await Task.Run(() => ExecuteDaySplit(plan)).ConfigureAwait(true);
            await MigrateMetadataPathsAsync(moved).ConfigureAwait(true);
            StatusMessage = failures.Count == 0
                ? $"已拆分：移动 {moved.Count:N0} 个文件到 {plan.TargetFolders.Count - 1} 个新文件夹。"
                : $"已移动 {moved.Count:N0} 个文件，{failures.Count:N0} 个没移动（目标已存在或被占用）。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"拆分中断：已移动 {moved.Count:N0} 个文件。{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressLabel = string.Empty;
        }

        if (failures.Count > 0)
            await PlatformServices.Dialogs.InfoAsync("部分文件没有移动", string.Join(Environment.NewLine, failures.Take(20))).ConfigureAwait(true);
        if (RefreshLibraryCommand.CanExecute(null)) await RefreshLibraryCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private string BuildDaySplitMessage(string folder, IReadOnlyList<LibraryDaySession> sessions, LibraryDaySplitPlan plan)
    {
        var message = new StringBuilder();
        message.AppendLine($"「{Path.GetFileName(folder)}」按拍摄间隔（≥ {ImportDaySplitHours} 小时）分成 {sessions.Count} 场：");
        message.AppendLine();
        for (var i = 0; i < sessions.Count; i++)
        {
            var target = Path.GetFileName(plan.TargetFolders[i]);
            var where = i == 0 ? $"留在「{target}」" : $"移到新文件夹「{target}」";
            message.AppendLine($"· {sessions[i].Remark}  {sessions[i].TimeText}：{plan.FilesPerFolder[i]:N0} 个文件，{where}");
        }
        message.AppendLine();
        message.AppendLine($"一共移动 {plan.Moves.Count:N0} 个文件（RAW / JPG / 视频 / XML 等同名文件一起走）。");
        message.AppendLine("「修后」文件夹不会移动；评分和标签跟着文件走；人物识别需要重新扫描这些照片。");
        message.Append("移动清单会记录在日志里。继续吗？");

        return message.ToString();
    }

    private static (IReadOnlyList<LibraryFileMove> Moved, List<string> Failures) ExecuteDaySplit(LibraryDaySplitPlan plan)
    {
        // 先写移动清单（出问题时可以照着还原）
        try
        {
            var journalDirectory = Path.Combine(AppDataPaths.Root, "day-split-journal");
            Directory.CreateDirectory(journalDirectory);
            File.WriteAllText(
                Path.Combine(journalDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Path.GetFileName(plan.DateFolder)}.json"),
                JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        var moved = new List<LibraryFileMove>(plan.Moves.Count);
        var failures = new List<string>();
        foreach (var move in plan.Moves)
        {
            try
            {
                if (File.Exists(move.Target))
                {
                    failures.Add($"{Path.GetFileName(move.Source)}：目标已存在");
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(move.Target)!);
                File.Move(move.Source, move.Target, overwrite: false);
                moved.Add(move);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add($"{Path.GetFileName(move.Source)}：{ex.Message}");
            }
        }
        return (moved, failures);
    }

    /// <summary>评分 / 标签 / 分类等元数据按路径存，移动后改成新路径。</summary>
    private async Task MigrateMetadataPathsAsync(IReadOnlyList<LibraryFileMove> moved)
    {
        if (moved.Count == 0) return;
        try
        {
            var map = moved.ToDictionary(move => Path.GetFullPath(move.Source), move => Path.GetFullPath(move.Target), StringComparer.OrdinalIgnoreCase);
            var snapshot = await _mediaMetadataStore.LoadAsync().ConfigureAwait(true);
            var changed = false;
            foreach (var entry in snapshot.Entries)
            {
                if (map.TryGetValue(Path.GetFullPath(entry.Path), out var target))
                {
                    entry.Path = target;
                    changed = true;
                }
            }
            if (changed) await _mediaMetadataStore.SaveAsync(snapshot).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[DaySplit] metadata migrate failed: {ex.Message}");
        }
    }
}

/// <summary>日期导航里日期文件夹下的一场（虚拟分组）。</summary>
public sealed class LibraryDateSessionNode(LibraryDateNode folder, LibraryDaySession session, string sectionKey)
{
    public LibraryDateNode Folder { get; } = folder;
    public LibraryDaySession Session { get; } = session;
    public string SectionKey { get; } = sectionKey;
    public string Title => Session.Remark;
    public string TimeText => Session.TimeText;
    public int Count => Session.Count;
    public string ToolTip => string.IsNullOrEmpty(Session.GapText)
        ? $"{Session.TimeText} · {Session.Count:N0} 个文件"
        : $"{Session.TimeText} · {Session.Count:N0} 个文件 · {Session.GapText}";
}
