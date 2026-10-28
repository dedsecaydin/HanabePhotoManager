using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Core.Sessions;
using HanabePhotoManager.Infrastructure.Sessions;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<ImportSourceStatus> ImportSourceCards { get; } = [];
    public ObservableCollection<ImportDeviceChoice> ImportDeviceChoices { get; } = [];
    public ObservableCollection<string> ImportSessionBuckets { get; } = [];
    public IEnumerable<CategoryChoice> ImportDropCategories => CategoryChoices.Where(c => ConcreteCategoryFolders.ContainsKey(c.Category));
    private string _importFilter = "";
    private string _importSourceFilter = "";
    private string _newImportSessionName = "";
    private readonly Stack<(ImportPreviewItemViewModel Item, CategoryChoice Category, string Session)[]> _organizationUndo = new();
    private bool _updatingOrganization;
    private string _importSessionBatchId = Guid.NewGuid().ToString("N");
    private Task PersistImportedSessionAsync(ImportResumeEntry entry, string? destination, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(entry.SessionName)) return Task.CompletedTask;
        destination ??= entry.Plan?.Files.FirstOrDefault(f => string.Equals(f.Source.FullPath, entry.PrimaryPath, StringComparison.OrdinalIgnoreCase))?.DestinationPath;
        if (destination is null) throw new IOException("场次保存缺少已导入目标，恢复记录已保留。");
        var path = destination;
        return Task.Run(() => ImportSessionWriter.AssignAsync(new JsonSessionStore(ImportSessionWriter.StorePath(entry.SessionLibraryRoot ?? LibraryRoot)),
            new ExifCaptureTimeReader(), entry.SessionName, path, token, entry.SessionBatchId ?? "legacy"), token);
    }
    public bool CanEditImportOrganization => !IsBusy && !IsImportDateFolderPreflightVisible;
    public string ImportSearchText { get => _importFilter; set { if (SetProperty(ref _importFilter, value)) RebuildImportSections(); } }
    public string NewImportSessionName { get => _newImportSessionName; set => SetProperty(ref _newImportSessionName, value); }
    public string ImportWorkspaceSummary => $"{ImportSourceCards.Count} 个来源 · {ImportItems.Count} 组素材 · {ImportItems.Count(i => i.IsSelected)} 组待导入";

    [RelayCommand]
    private void RefreshImportDevices()
    {
        if (IsBusy) return;
        RefreshConnectedDevices();
        UpdateImportDeviceChoices();
    }
    private void UpdateImportDeviceChoices()
    {
        var selected = ImportDeviceChoices.Where(d => d.IsSelected).Select(d => d.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ImportDeviceChoices.Clear();
        foreach (var device in ConnectedDevices.Where(d => d.IsConnected &&
                     !string.Equals(d.Path.TrimEnd('\\'), LibraryRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            ImportDeviceChoices.Add(new(device.Name, device.Path) { IsSelected = selected.Contains(device.Path) });
    }

    [RelayCommand]
    private async Task ScanSelectedImportDevicesAsync()
    {
        if (IsBusy) return;
        await PrepareMultipleImportSourcesAsync(ImportDeviceChoices.Where(d => d.IsSelected).Select(d => d.Path));
    }

    public async Task PrepareMultipleImportSourcesAsync(IEnumerable<string> paths)
    {
        if (IsBusy) return;
        var roots = NormalizeImportRoots(paths);
        if (roots.Length == 0) { ImportActionHint = "先勾选要扫描的设备，或选择多个文件夹。"; return; }
        if (HasLibraryRoot && roots.Any(p => IsPathWithin(LibraryRoot, p)))
        { ImportActionHint = "目标图库不能位于来源目录内，请调整来源或图库。"; return; }
        SourceFolder = ResolveDroppedSourceDisplayPath(roots);
        SetImportSourcePaths(roots);
        await AnalyzeSourcePathsAsync(roots, SourceFolder);
    }

    internal static string[] NormalizeImportRoots(IEnumerable<string> paths)
    {
        var roots = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Path.GetFullPath)
            .Select(Path.TrimEndingDirectorySeparator).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p.Length).ToArray();
        return roots.Where(p => !roots.Any(parent => parent.Length < p.Length && IsPathWithin(p, parent))).ToArray();
    }
    private static bool IsPathWithin(string path, string root) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), root, StringComparison.OrdinalIgnoreCase)
        || Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private async Task<FileInfo[]> ScanImportWorkspaceAsync(IReadOnlyList<string> paths, CancellationToken token,
        IProgress<ImportFileScanProgress> progress, IReadOnlySet<string> extensions, IReadOnlyList<ImportRoutingRule> rules)
    {
        ImportSourceCards.Clear();
        foreach (var path in NormalizeImportRoots(paths)) ImportSourceCards.Add(new(path));
        var cards = ImportSourceCards.ToArray();
        var files = new System.Collections.Concurrent.ConcurrentBag<FileInfo>();
        IProgress<(ImportSourceStatus Card, string State, int Count)> updates = new Progress<(ImportSourceStatus Card, string State, int Count)>(update =>
        {
            if (token.IsCancellationRequested) return;
            update.Card.State = update.State; update.Card.Count = update.Count;
        });
        await Parallel.ForEachAsync(cards, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = token }, (card, ct) =>
        {
            updates.Report((card, "扫描中", 0));
            if (!Directory.Exists(card.Path)) { updates.Report((card, "已断开 / 不可读取", 0)); return ValueTask.CompletedTask; }
            var found = EnumerateImportFileInfos([card.Path], ct, null, extensions, rules).ToArray();
            foreach (var file in found) files.Add(file);
            updates.Report((card, "已扫描", found.Length));
            progress.Report(new ImportFileScanProgress(card.Path, files.Count));
            return ValueTask.CompletedTask;
        });
        return files.DistinctBy(f => f.FullName, StringComparer.OrdinalIgnoreCase).OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    [RelayCommand] private void FilterImportSource(string? path) { _importSourceFilter = path ?? ""; RebuildImportSections(); }
    private bool MatchesImportFilter(ImportPreviewItemViewModel item) =>
        (string.IsNullOrEmpty(_importSourceFilter) || IsPathWithin(item.PrimaryPath, _importSourceFilter)) &&
        (string.IsNullOrWhiteSpace(ImportSearchText) || item.PrimaryPath.Contains(ImportSearchText, StringComparison.OrdinalIgnoreCase) || item.ImportSessionName.Contains(ImportSearchText, StringComparison.OrdinalIgnoreCase));

    [RelayCommand] private void MarkVisibleImportItems() { foreach (var item in ImportItems.Where(MatchesImportFilter)) item.IsOrganizeSelected = true; }
    [RelayCommand] private void ClearImportMarks() { foreach (var item in ImportItems) item.IsOrganizeSelected = false; }
    [RelayCommand] private void AddImportSession()
    {
        var name = NewImportSessionName.Trim();
        if (!CanEditImportOrganization || name.Length == 0 || name.Length > 80) return;
        if (!ImportSessionBuckets.Contains(name)) ImportSessionBuckets.Add(name);
        NewImportSessionName = "";
    }
    [RelayCommand] private async Task SuggestImportSessionsAsync()
    {
        if (!CanEditImportOrganization || ImportItems.Count == 0) return;
        using var cancellation = BeginCancelableTask(ActiveTaskKind.Analysis);
        var token = cancellation.Token;
        var items = ImportItems.ToArray();
        IsBusy = true;
        try
        {
            ProgressLabel = "读取各来源拍摄时间，预览场次…";
            var preview = await Task.Run(() =>
            {
                var reader = new ExifCaptureTimeReader();
                var members = new List<CaptureMember>();
                foreach (var item in items) { token.ThrowIfCancellationRequested(); members.Add(reader.Read(item.PrimaryPath)); }
                return SessionPlanner.Detect(members, TimeSpan.FromMinutes(30));
            }, token);
            token.ThrowIfCancellationRequested();
            _organizationUndo.Push(items.Select(i => (i, i.SelectedCategory, i.ImportSessionName)).ToArray());
            var byPath = items.ToDictionary(i => i.PrimaryPath, StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var group in preview.Groups)
            {
                var name = group.NeedsReview ? $"待确认 · {++index}" : $"场次 {++index} · {group.Start:MM-dd HH:mm}";
                if (!ImportSessionBuckets.Contains(name)) ImportSessionBuckets.Add(name);
                foreach (var member in group.Members) if (byPath.TryGetValue(member.Path, out var item)) item.ImportSessionName = name;
            }
            ImportActionHint = $"已生成 {preview.Groups.Count} 个场次建议（间隔 30 分钟）；待确认组请手动调整，可撤销。尚未保存。";
        }
        catch (OperationCanceledException) { ImportActionHint = "场次识别已停止，原方案保持不变。"; }
        catch (Exception ex) { ImportActionHint = "场次识别失败：" + ex.Message; }
        finally { EndCancelableTask(cancellation); IsBusy = false; RebuildImportSections(); }
    }
    [RelayCommand] private void AssignImportCategory(CategoryChoice? category) => AssignImportOrganization(ImportItems.Where(i => i.IsOrganizeSelected).ToArray(), category, null);
    [RelayCommand] private void AssignImportSession(string? session) => AssignImportOrganization(ImportItems.Where(i => i.IsOrganizeSelected).ToArray(), null, session);
    public void AssignImportOrganization(IReadOnlyList<ImportPreviewItemViewModel> items, CategoryChoice? category, string? session)
    {
        if (!CanEditImportOrganization || items.Count == 0 || items.Any(i => !ImportItems.Contains(i))) return;
        if (category is not null && !ImportDropCategories.Contains(category)) return;
        if (session is not null && session.Length > 0 && !ImportSessionBuckets.Contains(session)) return;
        _organizationUndo.Push(items.Select(i => (i, i.SelectedCategory, i.ImportSessionName)).ToArray());
        _updatingOrganization = true;
        try { foreach (var item in items) { if (category is not null) item.SelectedCategory = category; if (session is not null) item.ImportSessionName = session; } }
        finally { _updatingOrganization = false; }
        RebuildImportSections();
        ImportActionHint = $"已调整 {items.Count} 组素材的{(category is null ? "场次" : "分类")}，可撤销；原文件未修改。";
    }
    [RelayCommand] private void UndoImportOrganization()
    {
        if (!CanEditImportOrganization || !_organizationUndo.TryPop(out var previous)) return;
        _updatingOrganization = true;
        try { foreach (var row in previous.Where(r => ImportItems.Contains(r.Item))) { row.Item.SelectedCategory = row.Category; row.Item.ImportSessionName = row.Session; } }
        finally { _updatingOrganization = false; }
        RebuildImportSections(); ImportActionHint = "已撤销最近一次分类或场次调整。";
    }
    private void ResetImportOrganization()
    {
        _importSessionBatchId = Guid.NewGuid().ToString("N");
        _organizationUndo.Clear(); ImportSessionBuckets.Clear(); _importSourceFilter = "";
        ImportSearchText = "";
    }
}

public sealed partial class ImportDeviceChoice(string name, string path) : ObservableObject
{
    public string Name { get; } = name;
    public string Path { get; } = path;
    [ObservableProperty] private bool isSelected;
}
public sealed partial class ImportSourceStatus(string path) : ObservableObject
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Path)) is { Length: > 0 } name ? name : Path;
    [ObservableProperty] private string state = "等待扫描";
    [ObservableProperty] private int count;
}
