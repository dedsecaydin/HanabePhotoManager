using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public sealed class MapPhotosViewModel : ObservableObject
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp"], StringComparer.OrdinalIgnoreCase);
    private readonly IMediaMetadataStore _store;
    private readonly PhotoLocationService _locations;
    private readonly Func<IEnumerable<string>> _pathProvider;
    private readonly IExifLocationReader _exifReader;
    private readonly MapMediaSourceService _sourceService;
    private string _pendingLatitude = string.Empty;
    private string _pendingLongitude = string.Empty;
    private string _pendingDisplayName = string.Empty;
    private string _statusText = "尚未建立位置索引";
    private bool _isBusy;
    private readonly Dictionary<string, IReadOnlyList<string>> _markerPaths = new(StringComparer.Ordinal);

    public MapPhotosViewModel(
        IMediaMetadataStore store,
        Func<IEnumerable<string>> pathProvider,
        IExifLocationReader? exifReader = null,
        MapMediaSourceService? sourceService = null)
    {
        _store = store;
        _locations = new PhotoLocationService(store);
        _pathProvider = pathProvider;
        _exifReader = exifReader ?? new ExifLocationReader();
        _sourceService = sourceService ?? new MapMediaSourceService();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        AssignSelectedCommand = new AsyncRelayCommand(AssignSelectedAsync, () => !IsBusy);
        ClearSelectedManualCommand = new AsyncRelayCommand(ClearSelectedManualAsync, () => !IsBusy);
    }

    public ObservableCollection<MapPhotoItemViewModel> LocatedPhotos { get; } = [];
    public ObservableCollection<MapPhotoItemViewModel> UnlocatedPhotos { get; } = [];
    public ObservableCollection<MapMarkerPayload> Markers { get; } = [];
    public ObservableCollection<MapPhotoItemViewModel> SelectedLocationPhotos { get; } = [];
    public event EventHandler? MarkersChanged;

    public string PendingLatitude { get => _pendingLatitude; set => SetProperty(ref _pendingLatitude, value ?? string.Empty); }
    public string PendingLongitude { get => _pendingLongitude; set => SetProperty(ref _pendingLongitude, value ?? string.Empty); }
    public string PendingDisplayName { get => _pendingDisplayName; set => SetProperty(ref _pendingDisplayName, value ?? string.Empty); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            RefreshCommand.NotifyCanExecuteChanged();
            AssignSelectedCommand.NotifyCanExecuteChanged();
            ClearSelectedManualCommand.NotifyCanExecuteChanged();
        }
    }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AssignSelectedCommand { get; }
    public IAsyncRelayCommand ClearSelectedManualCommand { get; }

    public void SelectMarker(string markerId)
    {
        SelectedLocationPhotos.Clear();
        if (!_markerPaths.TryGetValue(markerId, out var paths)) return;
        var lookup = LocatedPhotos.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
            if (lookup.TryGetValue(path, out var item)) SelectedLocationPhotos.Add(item);
    }

    public IReadOnlyList<string> GetMarkerPhotoPaths(string markerId, int maximum = 40) =>
        _markerPaths.TryGetValue(markerId, out var paths)
            ? paths.Take(Math.Clamp(maximum, 0, 100)).ToArray()
            : [];

    public void SelectAllUnlocated()
    {
        foreach (var item in UnlocatedPhotos) item.IsSelected = true;
    }

    public void InvertUnlocatedSelection()
    {
        foreach (var item in UnlocatedPhotos) item.IsSelected = !item.IsSelected;
    }

    public void ClearSelection()
    {
        foreach (var item in LocatedPhotos.Concat(UnlocatedPhotos)) item.IsSelected = false;
    }

    public async Task RemoveSelectedSourcesAsync(CancellationToken cancellationToken = default)
    {
        var selected = LocatedPhotos.Concat(UnlocatedPhotos)
            .Where(item => item.IsSelected)
            .Select(item => Path.GetFullPath(item.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0) { StatusText = "请先选择要移出地图列表的照片。"; return; }
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
        snapshot.MapSourcePaths ??= [];
        snapshot.MapSourcePaths.RemoveAll(path => selected.Contains(Path.GetFullPath(path)));
        await _store.SaveAsync(snapshot, cancellationToken).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        StatusText = $"已从地图工作列表移出 {selected.Count:N0} 张照片；磁盘文件未删除。";
    }

    public async Task ClearImportedSourcesAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
        snapshot.MapSourcePaths ??= [];
        var count = snapshot.MapSourcePaths.Count;
        snapshot.MapSourcePaths.Clear();
        await _store.SaveAsync(snapshot, cancellationToken).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        StatusText = $"已清空 {count:N0} 个地图导入项；磁盘文件未删除。";
    }

    public async Task AddSourcesAsync(IEnumerable<string> paths, bool recursive, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            StatusText = "正在扫描选择的照片…";
            var scan = await _sourceService.ScanAsync(paths, recursive, cancellationToken).ConfigureAwait(true);
            var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
            snapshot.MapSourcePaths ??= [];
            var known = snapshot.MapSourcePaths.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in scan.Files.Where(known.Add)) snapshot.MapSourcePaths.Add(path);
            await _store.SaveAsync(snapshot, cancellationToken).ConfigureAwait(true);
            StatusText = scan.Warnings.Count == 0
                ? $"已加入 {scan.Files.Count:N0} 张照片"
                : $"已加入 {scan.Files.Count:N0} 张照片，{scan.Warnings.Count:N0} 个路径无法读取";
        }
        finally { IsBusy = false; }
        await RefreshAsync().ConfigureAwait(true);
    }

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var snapshot = await _store.LoadAsync().ConfigureAwait(true);
            snapshot.MapSourcePaths ??= [];
            var paths = _pathProvider().Concat(snapshot.MapSourcePaths).Where(File.Exists)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            snapshot.Entries ??= [];
            var lookup = snapshot.Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .GroupBy(entry => Path.GetFullPath(entry.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
            var changed = false;
            for (var index = 0; index < paths.Length; index++)
            {
                var path = paths[index];
                if (!lookup.TryGetValue(path, out var entry))
                {
                    entry = new MediaMetadataEntry { Path = path };
                    snapshot.Entries.Add(entry);
                    lookup[path] = entry;
                    changed = true;
                }
                if (entry.ExifLocation is null)
                {
                    var coordinate = _exifReader.TryRead(path);
                    if (coordinate is not null)
                    {
                        entry.ExifLocation = new PhotoLocation(
                            coordinate.Latitude, coordinate.Longitude, PhotoLocationSource.Exif);
                        changed = true;
                    }
                }
                StatusText = $"正在读取 EXIF 位置 {index + 1}/{paths.Length}";
            }
            if (changed) await _store.SaveAsync(snapshot).ConfigureAwait(true);
            Rebuild(paths, snapshot);
        }
        finally { IsBusy = false; }
    }

    public async Task AssignSelectedAsync()
    {
        var selected = LocatedPhotos.Concat(UnlocatedPhotos).Where(item => item.IsSelected).Select(item => item.Path).ToArray();
        if (selected.Length == 0) { StatusText = "请先勾选要定位的照片。"; return; }
        if (!TryParseCoordinate(PendingLatitude, out var latitude)
            || !TryParseCoordinate(PendingLongitude, out var longitude)
            || ExifLocationReader.Validate(latitude, longitude) is null)
        {
            StatusText = "请输入有效纬度（-90~90）和经度（-180~180）。";
            return;
        }
        await _locations.AssignManualAsync(selected, latitude, longitude, PendingDisplayName, default).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        StatusText = $"已为 {selected.Length} 张照片保存手动位置。";
    }

    public async Task ClearSelectedManualAsync()
    {
        var selected = LocatedPhotos.Where(item => item.IsSelected && item.Source == PhotoLocationSource.Manual)
            .Select(item => item.Path).ToArray();
        if (selected.Length == 0) { StatusText = "请选择带手动位置的照片。"; return; }
        await _locations.ClearManualAsync(selected, default).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        StatusText = $"已清除 {selected.Length} 张照片的手动位置。";
    }

    public void SetPendingMapPoint(double latitude, double longitude)
    {
        if (ExifLocationReader.Validate(latitude, longitude) is null) return;
        PendingLatitude = latitude.ToString("F6", CultureInfo.InvariantCulture);
        PendingLongitude = longitude.ToString("F6", CultureInfo.InvariantCulture);
        StatusText = "已从地图取点；勾选照片后点击“保存位置”。";
    }

    private void Rebuild(IReadOnlyCollection<string> paths, MediaMetadataSnapshot snapshot)
    {
        LocatedPhotos.Clear();
        UnlocatedPhotos.Clear();
        var lookup = snapshot.Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .GroupBy(entry => Path.GetFullPath(entry.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var located = new List<LocatedPhoto>();
        foreach (var path in paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            lookup.TryGetValue(path, out var entry);
            var location = entry?.EffectiveLocation;
            var item = new MapPhotoItemViewModel(path, location);
            if (location is null) UnlocatedPhotos.Add(item);
            else { LocatedPhotos.Add(item); located.Add(new LocatedPhoto(path, location)); }
        }
        Markers.Clear();
        SelectedLocationPhotos.Clear();
        _markerPaths.Clear();
        var clusters = PhotoLocationService.Cluster(located, zoom: 9);
        for (var index = 0; index < clusters.Count; index++)
        {
            var cluster = clusters[index];
            var id = $"m{index + 1}";
            Markers.Add(new MapMarkerPayload(id, cluster.Latitude, cluster.Longitude, cluster.Count));
            _markerPaths[id] = cluster.PhotoPaths;
        }
        MarkersChanged?.Invoke(this, EventArgs.Empty);
        StatusText = $"位置索引完成 · 已定位 {LocatedPhotos.Count} · 待定位 {UnlocatedPhotos.Count}";
    }

    private static bool TryParseCoordinate(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
        || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
}

public sealed partial class MapPhotoItemViewModel : ObservableObject
{
    public MapPhotoItemViewModel(string path, PhotoLocation? location)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Location = location;
    }
    public string Path { get; }
    public string Name { get; }
    public PhotoLocation? Location { get; }
    public PhotoLocationSource? Source => Location?.Source;
    public string LocationText => Location is null ? "未定位" : $"{Location.Latitude:F5}, {Location.Longitude:F5}";
    [ObservableProperty] private bool _isSelected;
}

public sealed record MapMarkerPayload(string Id, double Latitude, double Longitude, int Count);
