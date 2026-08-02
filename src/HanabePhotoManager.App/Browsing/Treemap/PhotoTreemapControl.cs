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
    private const double MinimumThumbnailArea = 12_000;
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

    public PhotoTreemapControl()
    {
        Focusable = true;
        SnapsToDevicePixels = true;
        AutomationProperties.SetName(this, "照片空间树图");
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

        var surface = FindBrush("Brush.Background.Canvas", WpfSystemColors.WindowBrush);
        drawingContext.DrawRectangle(surface, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var regions = new List<TreemapHitRegion>();
        var bounds = new TreemapBounds(0, 0, ActualWidth, ActualHeight);
        if (RootKey is null)
        {
            DrawRoot(drawingContext, bounds, regions);
        }
        else
        {
            var children = ItemsSource.Where(item => item.ParentKey == RootKey).ToArray();
            DrawItems(drawingContext, children, bounds, regions, drawContainerHeader: false);
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
        ICollection<TreemapHitRegion> regions)
    {
        var categories = ItemsSource
            .Where(item => item.ParentKey is null && item.IsContainer)
            .ToArray();
        foreach (var categoryTile in CalculateLayout(categories, bounds))
        {
            DrawTile(drawingContext, categoryTile.Item, categoryTile.Bounds, true);
            regions.Add(categoryTile);

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
            DrawItems(drawingContext, children, childBounds, regions, drawContainerHeader: false);
        }
    }

    private void DrawItems(
        DrawingContext drawingContext,
        IReadOnlyList<TreemapItemViewModel> items,
        TreemapBounds bounds,
        ICollection<TreemapHitRegion> regions,
        bool drawContainerHeader)
    {
        foreach (var tile in CalculateLayout(items, bounds))
        {
            DrawTile(drawingContext, tile.Item, tile.Bounds, drawContainerHeader);
            regions.Add(tile);
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

        var minimumLabelWidth = ResourceDouble("Size.Control.Default", 36);
        if (rect.Width < minimumLabelWidth || rect.Height < minimumLabelWidth / 2)
        {
            return;
        }

        var label = drawContainerHeader && item.IsContainer
            ? $"{item.Label} · {FormatBytes(item.Length)}"
            : item.Label;
        var textBrush = FindBrush("Brush.Text.Primary", WpfSystemColors.ControlTextBrush);
        var fontSize = ResourceDouble("Typography.Caption", 12);
        var formatted = new FormattedText(
            label,
            CultureInfo.CurrentUICulture,
            WpfFlowDirection.LeftToRight,
            new Typeface(
                TryFindResource("Typography.FontFamily.UI") as MediaFontFamily ?? System.Windows.SystemFonts.MessageFontFamily,
                FontStyles.Normal,
                FontWeights.SemiBold,
                FontStretches.Normal),
            fontSize,
            textBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, rect.Width - gap * 4),
            MaxTextHeight = Math.Max(1, rect.Height - gap * 2),
            Trimming = TextTrimming.CharacterEllipsis
        };
        drawingContext.DrawText(formatted, new WpfPoint(rect.X + gap * 2, rect.Y + gap));
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
