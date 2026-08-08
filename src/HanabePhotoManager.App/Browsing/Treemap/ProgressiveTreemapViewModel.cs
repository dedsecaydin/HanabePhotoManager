using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.Core.Browsing.Treemap;
using System.IO;
using System.Windows.Media;

namespace HanabePhotoManager.App.Browsing.Treemap;

public sealed class ProgressiveTreemapViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMilliseconds(150);

    private readonly object _gate = new();
    private readonly Dictionary<string, LibraryDateMediaItem> _mediaByPath =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageSource> _thumbnailsByPath =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _dimensionsByPath =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _refreshInterval;
    private readonly SynchronizationContext? _synchronizationContext;
    private CancellationTokenSource? _pendingPublishCancellation;
    private IReadOnlyList<TreemapItemViewModel> _items = [];
    private IReadOnlyList<TreemapItemViewModel> _visibleItems = [];
    private IReadOnlyList<TreemapBreadcrumbViewModel> _breadcrumbs = [];
    private TreemapWeightMode _weightMode = TreemapWeightMode.FileSize;
    private string _rootPath = string.Empty;
    private string? _currentContainerKey;
    private int _generation;
    private int _discoveredCount;
    private int _layoutRevision;
    private bool _hasPublished;
    private bool _isScanning;
    private bool _isPartial;
    private bool _disposed;
    private int _isPublishing; // 1 = in progress, 0 = idle

    public ProgressiveTreemapViewModel(TimeSpan? refreshInterval = null)
    {
        _refreshInterval = refreshInterval ?? DefaultRefreshInterval;
        if (_refreshInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshInterval));
        }

        _synchronizationContext = SynchronizationContext.Current;
    }

    public IReadOnlyList<TreemapItemViewModel> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public IReadOnlyList<TreemapItemViewModel> VisibleItems
    {
        get => _visibleItems;
        private set => SetProperty(ref _visibleItems, value);
    }

    public IReadOnlyList<TreemapBreadcrumbViewModel> Breadcrumbs
    {
        get => _breadcrumbs;
        private set => SetProperty(ref _breadcrumbs, value);
    }

    public TreemapWeightMode WeightMode
    {
        get => _weightMode;
        set
        {
            if (SetProperty(ref _weightMode, value))
            {
                PublishNow(_generation);
            }
        }
    }

    public string RootPath
    {
        get => _rootPath;
        private set => SetProperty(ref _rootPath, value);
    }

    public string? CurrentContainerKey
    {
        get => _currentContainerKey;
        private set => SetProperty(ref _currentContainerKey, value);
    }

    public int DiscoveredCount
    {
        get => _discoveredCount;
        private set => SetProperty(ref _discoveredCount, value);
    }

    public int LayoutRevision
    {
        get => _layoutRevision;
        private set => SetProperty(ref _layoutRevision, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetProperty(ref _isScanning, value);
    }

    public bool IsPartial
    {
        get => _isPartial;
        private set => SetProperty(ref _isPartial, value);
    }

    public int BeginScan(string rootPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        CancelPendingPublish();
        lock (_gate)
        {
            _generation++;
            _mediaByPath.Clear();
            _thumbnailsByPath.Clear();
            _dimensionsByPath.Clear();
            _hasPublished = false;
        }

        RootPath = rootPath;
        CurrentContainerKey = null;
        DiscoveredCount = 0;
        IsScanning = true;
        IsPartial = false;
        Items = [];
        VisibleItems = [];
        Breadcrumbs = [new TreemapBreadcrumbViewModel(null, GetRootLabel(rootPath))];
        LayoutRevision++;
        return _generation;
    }

    public void ApplyBatch(int generation, LibraryDateSnapshotBatch batch)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(batch);

        var publishImmediately = false;
        lock (_gate)
        {
            if (generation != _generation)
            {
                return;
            }

            foreach (var item in batch.Items)
            {
                _mediaByPath.TryAdd(item.FullPath, item);
            }

            publishImmediately = !_hasPublished && _mediaByPath.Count > 0;
            _hasPublished |= publishImmediately;
        }

        if (publishImmediately)
        {
            PublishNow(generation);
        }
        else
        {
            SchedulePublish(generation);
        }
    }

    public void Complete(int generation, bool isPartial)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (generation != _generation)
            {
                return;
            }
        }

        CancelPendingPublish();
        IsScanning = false;
        IsPartial = isPartial;
        PublishNow(generation);
    }

    public void UpdateThumbnail(string fullPath, ImageSource? thumbnail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        lock (_gate)
        {
            if (!_mediaByPath.ContainsKey(fullPath))
            {
                return;
            }

            if (thumbnail is null)
            {
                _thumbnailsByPath.Remove(fullPath);
            }
            else
            {
                _thumbnailsByPath[fullPath] = thumbnail;
            }
        }

        PublishNow(_generation);
    }

    public void ZoomTo(string containerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerKey);
        if (!Items.Any(item => item.IsContainer && item.Key == containerKey))
        {
            return;
        }

        CurrentContainerKey = containerKey;
        PublishNow(_generation);
    }

    public void NavigateToAncestor(string? containerKey)
    {
        if (containerKey is not null &&
            !Items.Any(item => item.IsContainer && item.Key == containerKey))
        {
            return;
        }

        CurrentContainerKey = containerKey;
        PublishNow(_generation);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPendingPublish();
    }

    private void SchedulePublish(int generation)
    {
        CancelPendingPublish();
        var cancellation = new CancellationTokenSource();
        _pendingPublishCancellation = cancellation;
        _ = PublishAfterDelayAsync(generation, cancellation.Token);
    }

    private async Task PublishAfterDelayAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_refreshInterval, cancellationToken).ConfigureAwait(false);
            if (_synchronizationContext is not null)
            {
                _synchronizationContext.Post(_ => PublishNow(generation), null);
            }
            else
            {
                PublishNow(generation);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void PublishNow(int generation, bool useDimensions = false)
    {
        // Prevent recursive re-entry
        if (Interlocked.CompareExchange(ref _isPublishing, 1, 0) == 1)
        {
            return;
        }

        try
        {
            PublishNowCore(generation, useDimensions);
        }
        finally
        {
            Interlocked.Exchange(ref _isPublishing, 0);
        }
    }

    private void PublishNowCore(int generation, bool useDimensions)
    {
        LibraryDateMediaItem[] media;
        Dictionary<string, ImageSource> thumbnails;
        lock (_gate)
        {
            if (generation != _generation)
            {
                return;
            }

            media = _mediaByPath.Values
                .OrderBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            thumbnails = new Dictionary<string, ImageSource>(_thumbnailsByPath, StringComparer.OrdinalIgnoreCase);
        }

        var files = media
            .Select(item =>
            {
                var thumb = thumbnails.GetValueOrDefault(item.FullPath);
                // Use cached dimension from background reader, or thumbnail, or default
                var aspect = GetCachedAspectRatio(item.FullPath);
                if (aspect <= 1.0 && thumb is { Width: > 0, Height: > 0 })
                {
                    aspect = thumb.Width / thumb.Height;
                }

                return new TreemapItemViewModel(
                    item.FullPath,
                    CategoryKey(item.Category),
                    item.Name,
                    WeightMode == TreemapWeightMode.FileSize ? item.Length : 1,
                    false,
                    item.FullPath,
                    item.Length,
                    item.Category,
                    item.Extension,
                    thumb,
                    aspect);
            })
            .ToArray();
        var categories = files
            .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TreemapItemViewModel(
                CategoryKey(group.Key),
                null,
                group.Key,
                group.Sum(item => item.Weight),
                true,
                null,
                group.Sum(item => item.Length),
                group.Key,
                string.Empty))
            .OrderByDescending(item => item.Weight)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Items = [.. categories, .. files];
        VisibleItems = CurrentContainerKey is null
            ? categories
            : files.Where(item => item.ParentKey == CurrentContainerKey).ToArray();
        Breadcrumbs = CurrentContainerKey is null
            ? [new TreemapBreadcrumbViewModel(null, GetRootLabel(RootPath))]
            :
            [
                new TreemapBreadcrumbViewModel(null, GetRootLabel(RootPath)),
                new TreemapBreadcrumbViewModel(
                    CurrentContainerKey,
                    categories.First(item => item.Key == CurrentContainerKey).Label)
            ];
        DiscoveredCount = media.Length;
        LayoutRevision++;
    }

    private static double ResolveAspectRatio(string fullPath, System.Windows.Media.ImageSource? thumbnail)
    {
        // Prefer thumbnail dimensions if available (free, local property)
        if (thumbnail is { Width: > 0, Height: > 0 })
        {
            return thumbnail.Width / thumbnail.Height;
        }

        // Sensible default — background loader will populate real dimensions later
        return 1.5;
    }

    /// <summary>
    /// Submit pre-read dimensions from a background thread.
    /// Triggers a lightweight re-publish to update layouts.
    /// </summary>
    public void SubmitDimensions(IReadOnlyList<(string fullPath, double aspectRatio)> dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        if (_synchronizationContext is not null &&
            !ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            _synchronizationContext.Post(_ => SubmitDimensions(dimensions), null);
            return;
        }

        var changed = false;
        lock (_gate)
        {
            foreach (var (path, aspect) in dimensions)
            {
                if (double.IsFinite(aspect) && aspect > 0)
                {
                    if (!_dimensionsByPath.TryGetValue(path, out var current) ||
                        Math.Abs(current - aspect) > 0.0001)
                    {
                        _dimensionsByPath[path] = aspect;
                        changed = true;
                    }
                }
            }
        }

        if (changed && !_disposed && _mediaByPath.Count > 0)
        {
            PublishNow(_generation, useDimensions: true);
        }
    }

    private double GetCachedAspectRatio(string fullPath)
    {
        lock (_gate)
        {
            return _dimensionsByPath.TryGetValue(fullPath, out var aspect) ? aspect : 1.5;
        }
    }

    private void CancelPendingPublish()
    {
        var cancellation = Interlocked.Exchange(ref _pendingPublishCancellation, null);
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private static string CategoryKey(string category) => $"category:{category}";

    private static string GetRootLabel(string rootPath)
    {
        var trimmed = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed) is { Length: > 0 } name ? name : rootPath;
    }
}
