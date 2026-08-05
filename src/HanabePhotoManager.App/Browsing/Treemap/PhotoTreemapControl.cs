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
    private const double MinimumThumbnailArea = 400;
    private const double ViewportPadding = 20;
    private readonly SquarifiedTreemapLayout _layout = new();
    private IReadOnlyList<TreemapHitRegion> _hitRegions = [];

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

    public static readonly DependencyProperty OpenItemCommandProperty = DependencyProperty.Register(
        nameof(OpenItemCommand),
        typeof(ICommand),
        typeof(PhotoTreemapControl));

    public static readonly DependencyProperty ZoomCommandProperty = DependencyProperty.Register(
        nameof(ZoomCommand),
        typeof(ICommand),
        typeof(PhotoTreemapControl));

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

    public static bool ShouldRequestThumbnail(double width, double height) =>
        double.IsFinite(width) &&
        double.IsFinite(height) &&
        width >= 0 &&
        height >= 0 &&
        width * height >= MinimumThumbnailArea;

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
            return;
        }

        var visibleRect = VisibleRect.IsEmpty
            ? new Rect(0, 0, ActualWidth, ActualHeight)
            : VisibleRect;
        var padded = new Rect(
            visibleRect.X - ViewportPadding, visibleRect.Y - ViewportPadding,
            visibleRect.Width + ViewportPadding * 2, visibleRect.Height + ViewportPadding * 2);

        var surface = FindBrush("Brush.Background.Canvas", WpfSystemColors.WindowBrush);
        drawingContext.DrawRectangle(surface, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var regions = new List<TreemapHitRegion>();
        var bounds = new TreemapBounds(0, 0, ActualWidth, ActualHeight);
        if (RootKey is null)
        {
            DrawRoot(drawingContext, bounds, regions, padded);
        }
        else
        {
            var children = ItemsSource.Where(item => item.ParentKey == RootKey).ToArray();
            DrawItems(drawingContext, children, bounds, regions, drawContainerHeader: false, padded);
        }

        _hitRegions = regions;
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

    private void DrawRoot(
        DrawingContext drawingContext,
        TreemapBounds bounds,
        ICollection<TreemapHitRegion> regions,
        Rect visibleRect)
    {
        var categories = ItemsSource
            .Where(item => item.ParentKey is null && item.IsContainer)
            .ToArray();
        foreach (var categoryTile in CalculateLayout(categories, bounds))
        {
            regions.Add(categoryTile);

            var tileRect = new Rect(categoryTile.Bounds.X, categoryTile.Bounds.Y,
                categoryTile.Bounds.Width, categoryTile.Bounds.Height);
            if (!visibleRect.IntersectsWith(tileRect))
            {
                continue;
            }

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

            var childBounds = new TreemapBounds(
                categoryTile.Bounds.X + inset,
                categoryTile.Bounds.Y + headerHeight,
                childWidth,
                childHeight);
            var children = ItemsSource
                .Where(item => item.ParentKey == categoryTile.Item.Key)
                .ToArray();
            DrawItems(drawingContext, children, childBounds, regions, drawContainerHeader: false, visibleRect);
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
            regions.Add(tile);

            var tileRect = new Rect(tile.Bounds.X, tile.Bounds.Y,
                tile.Bounds.Width, tile.Bounds.Height);
            if (!visibleRect.IntersectsWith(tileRect))
            {
                continue;
            }

            DrawTile(drawingContext, tile.Item, tile.Bounds, drawContainerHeader);
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

        var isSelected = !item.IsContainer &&
            string.Equals(item.FullPath, SelectedPath, StringComparison.OrdinalIgnoreCase);
        var fill = item.IsContainer
            ? FindBrush("Brush.Surface.Default", WpfSystemColors.ControlBrush)
            : FindBrush("Brush.Surface.Subtle", WpfSystemColors.ControlLightBrush);
        var border = isSelected
            ? FindBrush("Brush.Border.Focus", WpfSystemColors.HighlightBrush)
            : FindBrush("Brush.Border.Default", WpfSystemColors.ControlDarkBrush);
        var radius = ResourceDouble("Radius.Control", 6);
        drawingContext.DrawRoundedRectangle(fill, new MediaPen(border, isSelected ? 2 : 1), rect, radius, radius);

        if (!item.IsContainer && item.Thumbnail is not null && ShouldRequestThumbnail(rect.Width, rect.Height))
        {
            DrawThumbnail(drawingContext, item.Thumbnail, rect, radius);
        }

        if (item.IsContainer || rect.Width < 40 || rect.Height < 20)
        {
            return;
        }

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
        drawingContext.DrawRoundedRectangle(
            badgeFill,
            null,
            new Rect(badgeX, badgeY, badgeWidth, badgeHeight),
            3, 3);

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
