using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public sealed class ImportSourcesViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<ImportSourceItemViewModel, FileSystemWatcher> _watchers = [];
    private readonly Func<Task> _save;
    private readonly Func<IReadOnlyList<ImportSourceSettings>, Task> _scan;
    private string _statusText = "可添加多个来源文件夹，重复及父子目录会自动跳过。";

    public ImportSourcesViewModel(Func<Task> save, Func<IReadOnlyList<ImportSourceSettings>, Task> scan)
    {
        _save = save;
        _scan = scan;
        RemoveCommand = new AsyncRelayCommand<ImportSourceItemViewModel>(RemoveAsync);
        RescanCommand = new AsyncRelayCommand<ImportSourceItemViewModel>(
            item => item is null ? Task.CompletedTask : _scan([item.ToSettings()]));
        ScanEnabledCommand = new AsyncRelayCommand(() => _scan(EnabledSources));
    }

    public ObservableCollection<ImportSourceItemViewModel> Items { get; } = [];
    public IAsyncRelayCommand<ImportSourceItemViewModel> RemoveCommand { get; }
    public IAsyncRelayCommand<ImportSourceItemViewModel> RescanCommand { get; }
    public IAsyncRelayCommand ScanEnabledCommand { get; }
    public IReadOnlyList<string> EnabledPaths =>
        ImportSourcePolicy.EnabledScanPaths(Items.Select(item => item.ToSettings()));
    public IReadOnlyList<ImportSourceSettings> EnabledSources
    {
        get
        {
            var paths = EnabledPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Items.Select(item => item.ToSettings())
                .Where(item => item.IsEnabled && paths.Contains(ImportSourcePolicy.Normalize(item.Path))).ToArray();
        }
    }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public void Load(IEnumerable<ImportSourceSettings> settings)
    {
        Clear();
        foreach (var setting in settings)
            AddItem(new ImportSourceItemViewModel(setting));
        RefreshWatchers();
    }

    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var settings = Items.Select(item => item.ToSettings()).ToList();
        var result = ImportSourcePolicy.AddRange(settings, paths.Where(Directory.Exists));
        if (result.Added > 0)
        {
            foreach (var setting in settings.Skip(Items.Count)) AddItem(new ImportSourceItemViewModel(setting));
            await _save().ConfigureAwait(true);
            RefreshWatchers();
        }
        StatusText = $"已添加 {result.Added} 个，跳过 {result.Rejected} 个重复或重叠目录。";
    }

    public List<ImportSourceSettings> Snapshot() => Items.Select(item => item.ToSettings()).ToList();

    private void AddItem(ImportSourceItemViewModel item)
    {
        item.PropertyChanged += Item_PropertyChanged;
        Items.Add(item);
    }

    private async void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImportSourceItemViewModel.IsEnabled)
            or nameof(ImportSourceItemViewModel.IncludeSubdirectories)
            or nameof(ImportSourceItemViewModel.AutoWatch))
        {
            RefreshWatchers();
            await _save().ConfigureAwait(true);
        }
    }

    private async Task RemoveAsync(ImportSourceItemViewModel? item)
    {
        if (item is null) return;
        item.PropertyChanged -= Item_PropertyChanged;
        if (_watchers.Remove(item, out var watcher)) watcher.Dispose();
        Items.Remove(item);
        await _save().ConfigureAwait(true);
        StatusText = "来源目录已从列表移除；磁盘文件未被删除。";
    }

    private void RefreshWatchers()
    {
        foreach (var watcher in _watchers.Values) watcher.Dispose();
        _watchers.Clear();
        foreach (var item in Items.Where(item => item.IsEnabled && item.AutoWatch && Directory.Exists(item.Path)))
        {
            var watcher = new FileSystemWatcher(item.Path)
            {
                IncludeSubdirectories = item.IncludeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            FileSystemEventHandler changed = (_, _) => QueueWatchScan(item);
            RenamedEventHandler renamed = (_, _) => QueueWatchScan(item);
            watcher.Created += changed;
            watcher.Changed += changed;
            watcher.Renamed += renamed;
            _watchers[item] = watcher;
        }
    }

    private void QueueWatchScan(ImportSourceItemViewModel item)
    {
        item.QueueWatchScan(async () =>
        {
            StatusText = $"检测到变更，正在后台重新扫描：{item.Path}";
            await _scan([item.ToSettings()]).ConfigureAwait(true);
        });
    }

    private void Clear()
    {
        foreach (var item in Items) item.PropertyChanged -= Item_PropertyChanged;
        Items.Clear();
        foreach (var watcher in _watchers.Values) watcher.Dispose();
        _watchers.Clear();
    }

    public void Dispose() => Clear();
}

public sealed class ImportSourceItemViewModel : ObservableObject, IDisposable
{
    private readonly System.Threading.Timer _watchDebounce;
    private Func<Task>? _pendingScan;
    private bool _isEnabled;
    private bool _includeSubdirectories;
    private bool _autoWatch;

    public ImportSourceItemViewModel(ImportSourceSettings settings)
    {
        Path = ImportSourcePolicy.Normalize(settings.Path);
        _isEnabled = settings.IsEnabled;
        _includeSubdirectories = settings.IncludeSubdirectories;
        _autoWatch = settings.AutoWatch;
        _watchDebounce = new System.Threading.Timer(_ => _ = RunPendingScanAsync());
    }

    public string Path { get; }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public bool IncludeSubdirectories { get => _includeSubdirectories; set => SetProperty(ref _includeSubdirectories, value); }
    public bool AutoWatch { get => _autoWatch; set => SetProperty(ref _autoWatch, value); }
    public string StateText => Directory.Exists(Path) ? "可用" : "目录不可用";
    public ImportSourceSettings ToSettings() => new()
    {
        Path = Path, IsEnabled = IsEnabled, IncludeSubdirectories = IncludeSubdirectories, AutoWatch = AutoWatch
    };

    public void QueueWatchScan(Func<Task> scan)
    {
        _pendingScan = scan;
        _watchDebounce.Change(TimeSpan.FromMilliseconds(750), Timeout.InfiniteTimeSpan);
    }

    private async Task RunPendingScanAsync()
    {
        var scan = Interlocked.Exchange(ref _pendingScan, null);
        if (scan is not null) await scan().ConfigureAwait(false);
    }

    public void Dispose() => _watchDebounce.Dispose();
}
