using HanabePhotoManager.Core.Browsing.Treemap;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaFontFamily = System.Windows.Media.FontFamily;
using MediaPen = System.Windows.Media.Pen;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfPoint = System.Windows.Point;
using WpfSystemColors = System.Windows.SystemColors;

namespace HanabePhotoManager.App.Browsing.Treemap;

public sealed class PhotoTreemapControl : FrameworkElement
{
    private const double ViewportPadding = 20;
    private static readonly bool DebugOverlay = false;
    private readonly SquarifiedTreemapLayout _layout = new();
    private readonly JustifiedGalleryLayout _galleryLayout = new(
        targetRowHeight: 180, minAspect: 0.35, maxAspect: 3.5, gap: 1);
    private readonly PanoramaPhotoLayout _panoramaLayout = new();
    private IReadOnlyList<TreemapHitRegion> _hitRegions = [];
    private int _debugThumbnailCount;
    private int _debugTileCount;
    private List<string> _visiblePaths = [];
    private List<string> _visibleWithoutThumbnail = [];
    private IReadOnlyList<TreemapItemViewModel>? _cachedPanoramaSource;
    private string? _cachedPanoramaRootKey;
    private IReadOnlyList<TreemapItemViewModel> _cachedPanoramaItems = [];
    private double _cachedPanoramaViewportWidth = double.NaN;
    private double _cachedPanoramaZoom = double.NaN;
    private PanoramaLayoutResult? _cachedPanoramaLayout;
    private double _contentHeight;
    private double _contentWidth;

    // Memoization for the custom-rendered treemap. The derived item groups
    // (root categories / per-category children) and the justified layouts depend
    // only on the ItemsSource identity, the active RootKey, and the container
    // width. Reusing them across scroll frames keeps OnRender O(visible) instead
    // of re-walking and re-laying out every item on each frame.
    private object? _layoutCacheItemsSource;
    private string? _layoutCacheRootKey;
    private double _layoutCacheWidth = double.NaN;
    private IReadOnlyList<TreemapItemViewModel>? _cachedRootCategories;
    private Dictionary<string, IReadOnlyList<TreemapItemViewModel>>? _cachedChildrenByCategory;
    private IReadOnlyList<JustifiedItem>? _cachedSubtreeLayout;
    private Dictionary<string, (double ChildWidth, IReadOnlyList<JustifiedItem> Items)>? _cachedCategoryLayouts;

    private string? _hoveredKey;

    /// <summary>
    /// Total content height of all items. Used by the code-behind's
    /// UpdateTreemapSize to grow the control beyond the viewport.
    /// </summary>
    internal double ContentHeight => _contentHeight;

    /// <summary>
    /// Total content width of all items. Used by the code-behind's
    /// UpdateTreemapSize to enable horizontal scrolling.
    /// </summary>
    internal double ContentWidth => _contentWidth;

    /// <summary>
    /// FullPaths of non-container tiles currently intersecting the visible rect,
    /// ordered by distance from viewport center (closest first).
    /// Populated during OnRender; read after render completes.
    /// </summary>
    internal IReadOnlyList<string> VisibleItemPaths => _visiblePaths;

    /// <summary>
    /// Subset of VisibleItemPaths whose Thumbnail is still null.
    /// </summary>
    internal IReadOnlyList<string> VisibleItemPathsNeedingThumbnail => _visibleWithoutThumbnail;

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IReadOnlyList<TreemapItemViewModel>),
        typeof(PhotoTreemapControl),
        new FrameworkPropertyMetadata(
            Array.Empty<TreemapItemViewModel>(),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RootKeyProperty = DependencyProperty.Register(
        nameof(RootKey),
        typeof(string),
        typeof(PhotoTreemapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedPathProperty = DependencyProperty.Register(
        nameof(SelectedPath),
        typeof(string),
        typeof(PhotoTreemapControl),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsRender |
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsBorderlessProperty = DependencyProperty.Register(
        nameof(IsBorderless),
        typeof(bool),
        typeof(PhotoTreemapControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OpenItemCommandProperty = DependencyProperty.Register(
        nameof(OpenItemCommand),
        typeof(ICommand),
        typeof(PhotoTreemapControl));

    public static readonly DependencyProperty ZoomCommandProperty = DependencyProperty.Register(
        nameof(ZoomCommand),
        typeof(ICommand),
        typeof(PhotoTreemapControl));

    public static readonly DependencyProperty ZoomScaleProperty = DependencyProperty.Register(
        nameof(ZoomScale),
        typeof(double),
        typeof(PhotoTreemapControl),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    internal static readonly DependencyProperty VisibleRectProperty = DependencyProperty.Register(
        nameof(VisibleRect),
        typeof(Rect),
        typeof(PhotoTreemapControl),
        new FrameworkPropertyMetadata(
            Rect.Empty,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public PhotoTreemapControl()
    {
        Focusable = true;
        SnapsToDevicePixels = true;
        AutomationProperties.SetName(this, "照片空间树图");
    }

    internal Rect VisibleRect
    {
        get => (Rect)GetValue(VisibleRectProperty);
        set => SetValue(VisibleRectProperty, value);
    }

    public IReadOnlyList<TreemapItemViewModel> ItemsSource
    {
        get => (IReadOnlyList<TreemapItemViewModel>)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string? RootKey
    {
        get => (string?)GetValue(RootKeyProperty);
        set => SetValue(RootKeyProperty, value);
    }

    public string? SelectedPath
    {
        get => (string?)GetValue(SelectedPathProperty);
        set => SetValue(SelectedPathProperty, value);
    }

    public bool IsBorderless
    {
        get => (bool)GetValue(IsBorderlessProperty);
        set => SetValue(IsBorderlessProperty, value);
    }

    public ICommand? OpenItemCommand
    {
        get => (ICommand?)GetValue(OpenItemCommandProperty);
        set => SetValue(OpenItemCommandProperty, value);
    }

    public ICommand? ZoomCommand
    {
        get => (ICommand?)GetValue(ZoomCommandProperty);
        set => SetValue(ZoomCommandProperty, value);
    }

    /// <summary>
    /// Current Ctrl+wheel scale. The lowest semantic band renders a dense
    /// panorama instead of shrinking ordinary treemap cells into noise.
    /// </summary>
    public double ZoomScale
    {
        get => (double)GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public static bool IsPanoramaZoom(double zoom) => PanoramaPhotoLayout.IsActive(zoom);

    internal PanoramaLayoutResult GetPanoramaLayout(double viewportWidth)
    {
        var photos = GetPanoramaItems();
        if (_cachedPanoramaLayout is not null &&
            _cachedPanoramaViewportWidth.Equals(viewportWidth) &&
            _cachedPanoramaZoom.Equals(ZoomScale))
        {
            return _cachedPanoramaLayout;
        }

        _cachedPanoramaViewportWidth = viewportWidth;
        _cachedPanoramaZoom = ZoomScale;
        _cachedPanoramaLayout = _panoramaLayout.Arrange(
            photos.Select(item => (item.AspectRatio, (string?)item.Key)).ToArray(),
            viewportWidth,
            ZoomScale);
        return _cachedPanoramaLayout;
    }

    public static bool ShouldRequestThumbnail(double width, double height) =>
        double.IsFinite(width) &&
        double.IsFinite(height) &&
        width >= 120 &&
        height >= 100;

    public static bool IntersectsViewport(Rect viewport, Rect tile) =>
        viewport.IntersectsWith(tile);

    public static TreemapItemViewModel? FindItemAt(
        IReadOnlyList<TreemapHitRegion> regions,
        double x,
        double y)
    {
        ArgumentNullException.ThrowIfNull(regions);
        for (var index = regions.Count - 1; index >= 0; index--)
        {
            var region = regions[index];
            if (x >= region.Bounds.X &&
                x <= region.Bounds.Right &&
                y >= region.Bounds.Y &&
                y <= region.Bounds.Bottom)
            {
                return region.Item;
            }
        }

        return null;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 1 || ActualHeight <= 1 || ItemsSource.Count == 0)
        {
            _hitRegions = [];
            if (ItemsSource.Count > 0)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[Treemap] OnRender skipped — Actual={ActualWidth:F0}x{ActualHeight:F0}, Items={ItemsSource.Count}");
            }

            return;
        }

        if (DebugOverlay)
        {
            var totalWithThumbnails = ItemsSource.Count(item => item.Thumbnail is not null);
            System.Diagnostics.Trace.WriteLine(
            $"[Treemap] OnRender — {ItemsSource.Count} items, {totalWithThumbnails} with thumbnail, " +
                $"canvas={ActualWidth:F0}x{ActualHeight:F0}, visible={VisibleRect}");
        }

        var visibleRect = VisibleRect.IsEmpty
            ? new Rect(0, 0, ActualWidth, ActualHeight)
            : VisibleRect;
        var padded = new Rect(
            visibleRect.X - ViewportPadding, visibleRect.Y - ViewportPadding,
            visibleRect.Width + ViewportPadding * 2, visibleRect.Height + ViewportPadding * 2);

        var surface = FindBrush("Brush.Background.Canvas", WpfSystemColors.WindowBrush);
        drawingContext.DrawRectangle(surface, null, new Rect(0, 0, ActualWidth, ActualHeight));

        _debugThumbnailCount = 0;
        _debugTileCount = 0;
        _visiblePaths = [];
        _visibleWithoutThumbnail = [];
        _contentWidth = ActualWidth;
        _contentHeight = ActualHeight;

        var regions = new List<TreemapHitRegion>();
        var bounds = new TreemapBounds(0, 0, ActualWidth, ActualHeight);
        if (IsPanoramaZoom(ZoomScale))
        {
            DrawPanorama(drawingContext, regions, padded);
        }
        else if (RootKey is null)
        {
            DrawRoot(drawingContext, bounds, regions, padded);
        }
        else
        {
            EnsureLayoutCache(bounds.Width);
            var children = ChildrenOf(RootKey!);
            if (children.Count > 0)
            {
                DrawSubtreeWithJustifiedLayout(drawingContext, children, bounds, regions, padded);
            }
        }

        _hitRegions = regions;

        if (DebugOverlay)
        {
            var catCount = ItemsSource.Count(item => item.IsContainer);
            System.Diagnostics.Trace.WriteLine(
                $"[Treemap-DEBUG] drawn={_debugTileCount} thumbs={_debugThumbnailCount} " +
                $"items={ItemsSource.Count} cats={catCount} " +
                $"viewport=({visibleRect.X:F0},{visibleRect.Y:F0})-({visibleRect.Width:F0}x{visibleRect.Height:F0}) " +
                $"canvas={ActualWidth:F0}x{ActualHeight:F0}");
        }
    }

    private IReadOnlyList<TreemapItemViewModel> GetPanoramaItems()
    {
        if (ReferenceEquals(_cachedPanoramaSource, ItemsSource) &&
            string.Equals(_cachedPanoramaRootKey, RootKey, StringComparison.Ordinal))
        {
            return _cachedPanoramaItems;
        }

        _cachedPanoramaSource = ItemsSource;
        _cachedPanoramaRootKey = RootKey;
        _cachedPanoramaItems = RootKey is null
            ? ItemsSource.Where(item => !item.IsContainer).ToArray()
            : ItemsSource.Where(item => item.ParentKey == RootKey && !item.IsContainer).ToArray();
        _cachedPanoramaViewportWidth = double.NaN;
        _cachedPanoramaZoom = double.NaN;
        _cachedPanoramaLayout = null;
        return _cachedPanoramaItems;
    }

    private void DrawPanorama(
        DrawingContext drawingContext,
        ICollection<TreemapHitRegion> regions,
        Rect visibleRect)
    {
        var photos = GetPanoramaItems();
        var panorama = GetPanoramaLayout(ActualWidth * ZoomScale);
        _contentWidth = panorama.ContentWidth;
        _contentHeight = Math.Max(ActualHeight, panorama.ContentHeight);
        var gap = ResourceDouble("Spacing.Hairline", 2);

        var (start, end) = VisibleRowRange(panorama.Items, visibleRect.Top, visibleRect.Bottom);
        for (var index = start; index < end && index < photos.Count; index++)
        {
            var item = photos[index];
            var layout = panorama.Items[index];
            var tileBounds = new TreemapBounds(layout.X, layout.Y, layout.Width + gap, layout.Height + gap);
            var tileRect = new Rect(layout.X, layout.Y, layout.Width, layout.Height);
            if (IntersectsViewport(visibleRect, tileRect))
            {
                DrawTile(drawingContext, item, tileBounds, drawContainerHeader: false);
                regions.Add(new TreemapHitRegion(item, tileBounds));
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var point = e.GetPosition(this);
        var item = FindItemAt(_hitRegions, point.X, point.Y);
        if (item is null)
        {
            return;
        }

        if (item.IsContainer)
        {
            if (ZoomCommand?.CanExecute(item.Key) == true)
            {
                ZoomCommand.Execute(item.Key);
            }
        }
        else
        {
            SelectedPath = item.FullPath;
            if (e.ClickCount >= 2 && OpenItemCommand?.CanExecute(item.FullPath) == true)
            {
                OpenItemCommand.Execute(item.FullPath);
            }
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(SelectedPath))
        {
            return;
        }

        if (OpenItemCommand?.CanExecute(SelectedPath) == true)
        {
            OpenItemCommand.Execute(SelectedPath);
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        var item = FindItemAt(_hitRegions, point.X, point.Y);
        var hoveredKey = item?.Key;
        if (string.Equals(_hoveredKey, hoveredKey, StringComparison.Ordinal))
        {
            return;
        }

        _hoveredKey = hoveredKey;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredKey is null)
        {
            return;
        }

        _hoveredKey = null;
        InvalidateVisual();
    }

    private void DrawRoot(
        DrawingContext drawingContext,
        TreemapBounds bounds,
        ICollection<TreemapHitRegion> regions,
        Rect visibleRect)
    {
        EnsureLayoutCache(bounds.Width);
        var categories = RootCategories;
        double maxRight = 0;
        double maxBottom = 0;
        foreach (var categoryTile in CalculateLayout(categories, bounds))
        {
            var tileRect = new Rect(categoryTile.Bounds.X, categoryTile.Bounds.Y,
                categoryTile.Bounds.Width, categoryTile.Bounds.Height);

            // Track full content bounds for scrolling
            var right = categoryTile.Bounds.X + categoryTile.Bounds.Width;
            var bottom = categoryTile.Bounds.Y + categoryTile.Bounds.Height;
            if (right > maxRight) maxRight = right;
            if (bottom > maxBottom) maxBottom = bottom;
            if (!IntersectsViewport(visibleRect, tileRect))
            {
                continue;
            }

            regions.Add(categoryTile);
            DrawTile(drawingContext, categoryTile.Item, categoryTile.Bounds, true);

            var headerHeight = Math.Min(
                ResourceDouble("Size.Control.Compact", 28),
                categoryTile.Bounds.Height * 0.35);
            var inset = ResourceDouble("Spacing.Hairline", 2);
            var childWidth = categoryTile.Bounds.Width - inset * 2;
            var childHeight = categoryTile.Bounds.Height - headerHeight - inset;
            if (childWidth <= 1 || childHeight <= 1)
            {
                continue;
            }

            var children = ChildrenOf(categoryTile.Item.Key);
            if (children.Count == 0) continue;

            // Semantic zoom: very small category cells stay as labelled area
            // summaries.  Rendering photo strips there is both illegible and
            // needlessly schedules thumbnail work.
            if (childWidth < 160 || childHeight < 120)
            {
                continue;
            }

            // Justified gallery layout for inner category tiles (memoized).
            var justifiedItems = GetCategoryLayout(categoryTile.Item.Key, children, childWidth);

            var childOffsetX = categoryTile.Bounds.X + inset;
            var childOffsetY = categoryTile.Bounds.Y + headerHeight;
            drawingContext.PushClip(new RectangleGeometry(
                new Rect(childOffsetX, childOffsetY, childWidth, childHeight)));
            var (start, end) = VisibleRowRange(
                justifiedItems,
                visibleRect.Top - childOffsetY,
                visibleRect.Bottom - childOffsetY);
            for (var i = start; i < end && i < children.Count; i++)
            {
                var child = children[i];
                var jItem = justifiedItems[i];
                var globalX = childOffsetX + jItem.X;
                var globalY = childOffsetY + jItem.Y;
                var childRect = new Rect(globalX, globalY, jItem.Width, jItem.Height);
                var tBounds = new TreemapBounds(globalX - inset / 2, globalY - inset / 2,
                    jItem.Width + inset, jItem.Height + inset);

                if (IntersectsViewport(visibleRect, childRect))
                {
                    DrawTile(drawingContext, child, tBounds, drawContainerHeader: false);
                    regions.Add(new TreemapHitRegion(child, tBounds));
                }
            }
            drawingContext.Pop();
        }
    }

    private void DrawItems(
        DrawingContext drawingContext,
        IReadOnlyList<TreemapItemViewModel> items,
        TreemapBounds bounds,
        ICollection<TreemapHitRegion> regions,
        bool drawContainerHeader,
        Rect visibleRect)
    {
        foreach (var tile in CalculateLayout(items, bounds))
        {
            var tileRect = new Rect(tile.Bounds.X, tile.Bounds.Y,
                tile.Bounds.Width, tile.Bounds.Height);
            if (!IntersectsViewport(visibleRect, tileRect))
            {
                continue;
            }

            regions.Add(tile);
            DrawTile(drawingContext, tile.Item, tile.Bounds, drawContainerHeader);
        }
    }

    private void DrawSubtreeWithJustifiedLayout(
        DrawingContext drawingContext,
        IReadOnlyList<TreemapItemViewModel> children,
        TreemapBounds bounds,
        ICollection<TreemapHitRegion> regions,
        Rect visibleRect)
    {
        var justifiedItems = GetSubtreeLayout(children, bounds.Width);
        var gap = ResourceDouble("Spacing.Hairline", 2);

        // Calculate full content dimensions
        var totalHeight = 0.0;
        var maxRight = 0.0;
        if (justifiedItems.Count > 0)
        {
            var last = justifiedItems[^1];
            totalHeight = last.Y + last.Height + gap;
            foreach (var ji in justifiedItems)
            {
                var r = ji.X + ji.Width;
                if (r > maxRight) maxRight = r;
            }
        }

        _contentHeight = totalHeight;
        if (maxRight > _contentWidth) _contentWidth = maxRight;

        var (start, end) = VisibleRowRange(justifiedItems, visibleRect.Top, visibleRect.Bottom);
        for (var i = start; i < end && i < children.Count; i++)
        {
            var child = children[i];
            var jItem = justifiedItems[i];
            var childRect = new Rect(jItem.X, jItem.Y, jItem.Width, jItem.Height);

            var tBounds = new TreemapBounds(
                jItem.X, jItem.Y,
                jItem.Width + gap, jItem.Height + gap);

            if (IntersectsViewport(visibleRect, childRect))
            {
                DrawTile(drawingContext, child, tBounds, drawContainerHeader: false);
                regions.Add(new TreemapHitRegion(child, tBounds));
            }
        }

        if (DebugOverlay)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[Treemap] Subtree layout: {children.Count} items, " +
                $"contentHeight={_contentHeight:F0}, viewportHeight={ActualHeight:F0}");
        }
    }

    private IReadOnlyList<TreemapHitRegion> CalculateLayout(
        IReadOnlyList<TreemapItemViewModel> items,
        TreemapBounds bounds)
    {
        var byKey = items.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        return _layout.Calculate(
                items.Select(item => new TreemapNode(item.Key, item.Label, item.Weight, item.IsContainer)).ToArray(),
                bounds)
            .Select(tile => new TreemapHitRegion(byKey[tile.Node.Key], tile.Bounds))
            .ToArray();
    }

    /// <summary>
    /// Resets the derived-group and layout memo caches whenever the ItemsSource
    /// identity, the active RootKey, or the container width changes.
    /// </summary>
    private void EnsureLayoutCache(double width)
    {
        if (ReferenceEquals(_layoutCacheItemsSource, ItemsSource) &&
            string.Equals(_layoutCacheRootKey, RootKey, StringComparison.Ordinal) &&
            _layoutCacheWidth.Equals(width))
        {
            return;
        }

        _layoutCacheItemsSource = ItemsSource;
        _layoutCacheRootKey = RootKey;
        _layoutCacheWidth = width;
        _cachedRootCategories = null;
        _cachedChildrenByCategory = null;
        _cachedSubtreeLayout = null;
        _cachedCategoryLayouts = null;
    }

    private IReadOnlyList<TreemapItemViewModel> RootCategories
    {
        get
        {
            _cachedRootCategories ??= ItemsSource
                .Where(item => item.ParentKey is null && item.IsContainer)
                .ToArray();
            return _cachedRootCategories;
        }
    }

    private IReadOnlyList<TreemapItemViewModel> ChildrenOf(string parentKey)
    {
        _cachedChildrenByCategory ??= new Dictionary<string, IReadOnlyList<TreemapItemViewModel>>(
            StringComparer.OrdinalIgnoreCase);
        if (!_cachedChildrenByCategory.TryGetValue(parentKey, out var children))
        {
            children = ItemsSource
                .Where(item => item.ParentKey == parentKey)
                .ToArray();
            _cachedChildrenByCategory[parentKey] = children;
        }

        return children;
    }

    private IReadOnlyList<JustifiedItem> GetSubtreeLayout(
        IReadOnlyList<TreemapItemViewModel> children,
        double width)
    {
        if (_cachedSubtreeLayout is not null)
        {
            return _cachedSubtreeLayout;
        }

        var aspects = children
            .Select(item => (aspectRatio: item.AspectRatio, key: (string?)item.Key))
            .ToArray();
        _cachedSubtreeLayout = _galleryLayout.Arrange(aspects, width);
        return _cachedSubtreeLayout;
    }

    private IReadOnlyList<JustifiedItem> GetCategoryLayout(
        string categoryKey,
        IReadOnlyList<TreemapItemViewModel> children,
        double childWidth)
    {
        _cachedCategoryLayouts ??= new Dictionary<string, (double ChildWidth, IReadOnlyList<JustifiedItem> Items)>(
            StringComparer.OrdinalIgnoreCase);
        if (_cachedCategoryLayouts.TryGetValue(categoryKey, out var entry) &&
            entry.ChildWidth.Equals(childWidth))
        {
            return entry.Items;
        }

        var aspects = children
            .Select(item => (aspectRatio: item.AspectRatio, key: (string?)item.Key))
            .ToArray();
        var arranged = _galleryLayout.Arrange(aspects, childWidth);
        _cachedCategoryLayouts[categoryKey] = (childWidth, arranged);
        return arranged;
    }

    /// <summary>
    /// Returns the index range [start, end) of a justified item list whose rows
    /// vertically overlap the span [topY, bottomY]. Justified rows are laid out
    /// top-to-bottom with monotonically non-decreasing Y, so a binary search
    /// skips the off-screen prefix instead of walking every item on each frame.
    /// </summary>
    private static (int Start, int End) VisibleRowRange(
        IReadOnlyList<JustifiedItem> items,
        double topY,
        double bottomY)
    {
        if (items.Count == 0)
        {
            return (0, 0);
        }

        var low = 0;
        var high = items.Count - 1;
        var start = items.Count;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (items[mid].Y + items[mid].Height >= topY)
            {
                start = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        var end = start;
        while (end < items.Count && items[end].Y <= bottomY)
        {
            end++;
        }

        return (start, end);
    }

    private void DrawTile(
        DrawingContext drawingContext,
        TreemapItemViewModel item,
        TreemapBounds bounds,
        bool drawContainerHeader)
    {
        var gap = ResourceDouble("Spacing.Hairline", 2);
        var rect = new Rect(
            bounds.X + gap / 2,
            bounds.Y + gap / 2,
            Math.Max(0, bounds.Width - gap),
            Math.Max(0, bounds.Height - gap));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        if (DebugOverlay && !item.IsContainer)
        {
            _debugTileCount++;
            if (item.Thumbnail is not null) _debugThumbnailCount++;
        }

        // Collect visible items for viewport-driven loading
        if (!item.IsContainer && !string.IsNullOrEmpty(item.FullPath))
        {
            _visiblePaths.Add(item.FullPath);
            if (item.Thumbnail is null) _visibleWithoutThumbnail.Add(item.FullPath);
        }

        var isSelected = !item.IsContainer &&
            string.Equals(item.FullPath, SelectedPath, StringComparison.OrdinalIgnoreCase);
        var isHovered = !item.IsContainer &&
            string.Equals(item.Key, _hoveredKey, StringComparison.Ordinal);

        // In justified/borderless mode, skip background fill for non-container tiles
        // so images flow seamlessly edge-to-edge
        var isPanorama = IsPanoramaZoom(ZoomScale);
        if (!item.IsContainer && (IsBorderless || isPanorama))
        {
            // No background fill — just draw image and extension badge
            if (item.Thumbnail is not null && CanDrawThumbnail(rect, isPanorama))
            {
                DrawThumbnail(drawingContext, item.Thumbnail, rect, 0);
            }

            if (isSelected)
            {
                var selBorder = FindBrush("Brush.Border.Focus", WpfSystemColors.HighlightBrush);
                drawingContext.DrawRectangle(null, new MediaPen(selBorder, 2), rect);
            }
            else if (isHovered)
            {
                var hoverBorder = FindBrush("Brush.Border.Strong", WpfSystemColors.ControlDarkBrush);
                drawingContext.DrawRectangle(null, new MediaPen(hoverBorder, 1.5), rect);
            }

            // Extension badge
            if (!isPanorama)
            {
                DrawExtensionBadge(drawingContext, item, rect, gap);
            }
            return;
        }

        var fill = item.IsContainer
            ? FindBrush("Brush.Surface.Default", WpfSystemColors.ControlBrush)
            : FindBrush(
                isHovered ? "Brush.Surface.Interactive" : "Brush.Surface.Subtle",
                WpfSystemColors.ControlLightBrush);
        var border = isSelected
            ? FindBrush("Brush.Border.Focus", WpfSystemColors.HighlightBrush)
            : FindBrush(
                isHovered ? "Brush.Border.Strong" : "Brush.Border.Default",
                WpfSystemColors.ControlDarkBrush);
        var radius = ResourceDouble("Radius.Control", 6);
        var drawBorder = !IsBorderless || isSelected;
        drawingContext.DrawRoundedRectangle(
            fill,
            drawBorder ? new MediaPen(border, isSelected ? 2 : 1) : null,
            rect, radius, radius);

        // Container header — draw category name at the top
        if (drawContainerHeader && item.IsContainer && !string.IsNullOrWhiteSpace(item.Label))
        {
            var headerHeight = Math.Min(
                ResourceDouble("Size.Control.Compact", 28),
                rect.Height * 0.35);
            if (headerHeight > 8)
            {
                var headerRect = new Rect(rect.X, rect.Y, rect.Width, headerHeight);
                var headerBg = FindBrush("Brush.Surface.Subtle", WpfSystemColors.ControlLightBrush);
                drawingContext.DrawRectangle(headerBg, null, headerRect);
                MediaPen? separator = null;
                try
                {
                    separator = new MediaPen(
                        FindBrush("Brush.Border.Subtle", WpfSystemColors.ControlDarkBrush),
                        0.7);
                }
                catch { }
                if (separator is not null)
                {
                    drawingContext.DrawLine(separator,
                        new WpfPoint(headerRect.Left, headerRect.Bottom),
                        new WpfPoint(headerRect.Right, headerRect.Bottom));
                }

                var headerFontSize = ResourceDouble("Typography.Caption", 10);
                var headerTextBrush = FindBrush("Brush.Text.Secondary", WpfSystemColors.GrayTextBrush);
                var headerFormatted = new FormattedText(
                    item.Label,
                    CultureInfo.CurrentUICulture,
                    WpfFlowDirection.LeftToRight,
                    new Typeface(
                        TryFindResource("Typography.FontFamily.UI") as MediaFontFamily ?? System.Windows.SystemFonts.MessageFontFamily,
                        FontStyles.Normal,
                        FontWeights.Normal,
                        FontStretches.Normal),
                    headerFontSize,
                    headerTextBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip)
                {
                    MaxTextWidth = Math.Max(1, headerRect.Width - 14),
                    MaxTextHeight = Math.Max(1, headerRect.Height - 4),
                    Trimming = TextTrimming.CharacterEllipsis
                };
                var textX = headerRect.X + 8;
                var textY = headerRect.Y + (headerRect.Height - headerFormatted.Height) / 2;
                drawingContext.DrawText(headerFormatted, new WpfPoint(textX, textY));
            }

            return; // Container tiles don't need extension badges
        }

        if (!item.IsContainer && item.Thumbnail is not null && CanDrawThumbnail(rect, isPanorama))
        {
            DrawThumbnail(drawingContext, item.Thumbnail, rect, radius);
        }

        if (item.IsContainer || rect.Width < 40 || rect.Height < 20)
        {
            return;
        }

        DrawExtensionBadge(drawingContext, item, rect, gap);

        if (DebugOverlay && !item.IsContainer)
        {
            // Green border = has thumbnail  /  Gray border = no thumbnail
            var debugBorderColor = item.Thumbnail is not null
                ? System.Windows.Media.Brushes.LimeGreen
                : System.Windows.Media.Brushes.Gray;
            drawingContext.DrawRectangle(
                null,
                new MediaPen(debugBorderColor, 1.2),
                new Rect(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2));
        }
    }

    private bool CanDrawThumbnail(Rect rect, bool isPanorama) =>
        isPanorama
            ? rect.Width * ZoomScale >= 24 && rect.Height * ZoomScale >= 24
            : ShouldRequestThumbnail(rect.Width, rect.Height);

    private void DrawExtensionBadge(DrawingContext drawingContext, TreemapItemViewModel item, Rect rect, double gap)
    {
        if (rect.Width < 40 || rect.Height < 20) return;

        var extension = item.Extension.Length > 0
            ? item.Extension.ToUpperInvariant()
            : string.Empty;
        if (extension.Length == 0) return;

        var fontSize = ResourceDouble("Typography.Caption", 9);
        var textBrush = FindBrush("Brush.Text.Primary", WpfSystemColors.ControlTextBrush);
        var formatted = new FormattedText(
            extension,
            CultureInfo.CurrentUICulture,
            WpfFlowDirection.LeftToRight,
            new Typeface(
                TryFindResource("Typography.FontFamily.UI") as MediaFontFamily ?? System.Windows.SystemFonts.MessageFontFamily,
                FontStyles.Normal,
                FontWeights.Bold,
                FontStretches.Normal),
            fontSize,
            textBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var badgeWidth = formatted.Width + 8;
        var badgeHeight = formatted.Height + 4;
        var badgeX = rect.Right - badgeWidth - gap;
        var badgeY = rect.Y + gap;

        var badgeFill = FindBrush("Brush.Surface.Subtle", WpfSystemColors.ControlBrush);
        drawingContext.DrawRoundedRectangle(badgeFill, null,
            new Rect(badgeX, badgeY, badgeWidth, badgeHeight), 3, 3);
        drawingContext.DrawText(formatted, new WpfPoint(badgeX + 4, badgeY + 2));
    }

    private static void DrawThumbnail(DrawingContext drawingContext, ImageSource thumbnail, Rect rect, double radius)
    {
        if (thumbnail.Width <= 0 || thumbnail.Height <= 0)
        {
            return;
        }

        var scale = Math.Max(rect.Width / thumbnail.Width, rect.Height / thumbnail.Height);
        var width = thumbnail.Width * scale;
        var height = thumbnail.Height * scale;
        var destination = new Rect(
            rect.X + (rect.Width - width) / 2,
            rect.Y + (rect.Height - height) / 2,
            width,
            height);
        drawingContext.PushClip(new RectangleGeometry(rect, radius, radius));
        drawingContext.DrawImage(thumbnail, destination);
        drawingContext.Pop();
    }

    private MediaBrush FindBrush(string key, MediaBrush fallback) =>
        TryFindResource(key) as MediaBrush ?? fallback;

    private double ResourceDouble(string key, double fallback) =>
        TryFindResource(key) is double value ? value : fallback;

    internal static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var value = bytes / 1024d;
        if (value < 1024)
        {
            return $"{value:0.#} KB";
        }

        value /= 1024;
        return value < 1024 ? $"{value:0.#} MB" : $"{value / 1024:0.#} GB";
    }
}

public sealed record TreemapHitRegion(TreemapItemViewModel Item, TreemapBounds Bounds)
{
    public string AutomationName => Item.IsContainer
        ? $"{Item.Label}，文件夹，{PhotoTreemapControl.FormatBytes(Item.Length)}"
        : $"{Item.Label}，{Item.Extension}，{PhotoTreemapControl.FormatBytes(Item.Length)}";
}
