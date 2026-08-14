using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Size = System.Windows.Size;
using Point = System.Windows.Point;

namespace HanabePhotoManager.App.Controls;

/// <summary>
/// A virtualizing WrapPanel that lays out uniformly-sized items left-to-right
/// and top-to-bottom while only realizing the containers that intersect the
/// current viewport. It implements <see cref="IScrollInfo"/> so the owning
/// <see cref="ScrollViewer"/> (with <c>CanContentScroll=True</c>) delegates
/// scrolling to it instead of measuring the whole content at once.
/// </summary>
/// <remarks>
/// WPF's built-in <see cref="VirtualizingStackPanel"/> virtualizes in a single
/// direction only and <see cref="WrapPanel"/> never virtualizes, so a people
/// detail page with hundreds of photos would otherwise realize every tile.
/// This panel keeps the responsive multi-column wrap layout while recycling
/// only the visible rows. The tiles are fixed-size (see <see cref="ItemWidth"/>
/// and <see cref="ItemHeight"/>), which the person photo grid already uses.
/// </remarks>
public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    private const double ScrollLineAmount = 16d;

    private Size _extent = new();
    private Size _viewport = new();
    private Point _offset = new();
    private ScrollViewer? _scrollOwner;

    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(142d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(142d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    private int ItemCount => ItemsControl.GetItemsOwner(this)?.Items.Count ?? 0;

    private ItemsControl? ItemsOwner => ItemsControl.GetItemsOwner(this);

    private ItemContainerGenerator? ContainerGenerator => ItemsOwner?.ItemContainerGenerator;

    private int ItemsPerRow => Math.Max(1, (int)Math.Floor(Math.Max(1d, ViewportWidth) / Math.Max(1d, ItemWidth)));

    // ---- IScrollInfo ----

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; } = true;

    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset => _offset.Y;

    public ScrollViewer? ScrollOwner
    {
        get => _scrollOwner;
        set => _scrollOwner = value;
    }

    public void LineUp() => SetVerticalOffset(VerticalOffset - ScrollLineAmount);
    public void LineDown() => SetVerticalOffset(VerticalOffset + ScrollLineAmount);
    public void LineLeft() => SetHorizontalOffset(HorizontalOffset - ScrollLineAmount);
    public void LineRight() => SetHorizontalOffset(HorizontalOffset + ScrollLineAmount);
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - 3 * ScrollLineAmount);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + 3 * ScrollLineAmount);
    public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - 3 * ScrollLineAmount);
    public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + 3 * ScrollLineAmount);
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth);
    public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);

    public void SetHorizontalOffset(double offset)
    {
        offset = Math.Clamp(offset, 0, Math.Max(0, ExtentWidth - ViewportWidth));
        if (Math.Abs(_offset.X - offset) < 0.001)
        {
            return;
        }

        _offset.X = offset;
        _scrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public void SetVerticalOffset(double offset)
    {
        offset = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        if (Math.Abs(_offset.Y - offset) < 0.001)
        {
            return;
        }

        _offset.Y = offset;
        _scrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        var element = visual as FrameworkElement;
        while (element is not null && !ReferenceEquals(element, this))
        {
            var itemIndex = ContainerGenerator?.IndexFromContainer(element) ?? -1;
            if (itemIndex >= 0)
            {
                var itemHeight = Math.Max(1d, ItemHeight);
                var row = itemIndex / ItemsPerRow;
                var top = row * itemHeight;
                if (top < VerticalOffset)
                {
                    SetVerticalOffset(top);
                }
                else if (top + itemHeight > VerticalOffset + ViewportHeight)
                {
                    SetVerticalOffset(top + itemHeight - ViewportHeight);
                }

                break;
            }

            element = VisualTreeHelper.GetParent(element) as FrameworkElement;
        }

        return rectangle;
    }

    // ---- Measure / Arrange ----

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateScrollInfo(availableSize);
        GetVisibleRange(out var firstVisibleItemIndex, out var lastVisibleItemIndex);

        var generator = ItemContainerGenerator;
        if (firstVisibleItemIndex < 0 || lastVisibleItemIndex < firstVisibleItemIndex)
        {
            CleanUpItems(0, -1);
            return ClampToFinite(availableSize);
        }

        var startPosition = generator.GeneratorPositionFromIndex(firstVisibleItemIndex);
        var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

        using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
        {
            for (var itemIndex = firstVisibleItemIndex; itemIndex <= lastVisibleItemIndex; itemIndex++, childIndex++)
            {
                var child = generator.GenerateNext(out var newlyRealized) as UIElement;
                if (newlyRealized && child is not null)
                {
                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else
                    {
                        InsertInternalChild(childIndex, child);
                    }

                    generator.PrepareItemContainer(child);
                }

                child?.Measure(new Size(Math.Max(1d, ItemWidth), Math.Max(1d, ItemHeight)));
            }
        }

        CleanUpItems(firstVisibleItemIndex, lastVisibleItemIndex);
        return ClampToFinite(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        GetVisibleRange(out var firstVisibleItemIndex, out var lastVisibleItemIndex);

        foreach (UIElement child in InternalChildren)
        {
            var itemIndex = ContainerGenerator?.IndexFromContainer(child) ?? -1;
            if (itemIndex < firstVisibleItemIndex || itemIndex > lastVisibleItemIndex)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            var itemWidth = Math.Max(1d, ItemWidth);
            var itemHeight = Math.Max(1d, ItemHeight);
            var row = itemIndex / ItemsPerRow;
            var column = itemIndex % ItemsPerRow;
            child.Arrange(new Rect(
                column * itemWidth - HorizontalOffset,
                row * itemHeight - VerticalOffset,
                child.DesiredSize.Width,
                child.DesiredSize.Height));
        }

        return finalSize;
    }

    private void UpdateScrollInfo(Size availableSize)
    {
        var extent = CalculateExtent(availableSize, ItemCount);
        if (extent != _extent)
        {
            _extent = extent;
            _scrollOwner?.InvalidateScrollInfo();
        }

        if (availableSize != _viewport)
        {
            _viewport = availableSize;
            _scrollOwner?.InvalidateScrollInfo();
        }
    }

    private Size CalculateExtent(Size availableSize, int itemCount)
    {
        var viewportWidth = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0
            ? Math.Max(1d, ItemWidth)
            : availableSize.Width;
        var viewportHeight = double.IsInfinity(availableSize.Height) || availableSize.Height <= 0
            ? Math.Max(1d, ItemHeight)
            : availableSize.Height;

        if (itemCount == 0)
        {
            return new Size(viewportWidth, 0);
        }

        var itemWidth = Math.Max(1d, ItemWidth);
        var itemHeight = Math.Max(1d, ItemHeight);
        var itemsPerRow = Math.Max(1, (int)Math.Floor(viewportWidth / itemWidth));
        var rowCount = (int)Math.Ceiling((double)itemCount / itemsPerRow);
        return new Size(viewportWidth, Math.Max(rowCount * itemHeight, viewportHeight));
    }

    private void GetVisibleRange(out int firstVisibleItemIndex, out int lastVisibleItemIndex)
    {
        var itemCount = ItemCount;
        if (itemCount == 0)
        {
            firstVisibleItemIndex = -1;
            lastVisibleItemIndex = -1;
            return;
        }

        var itemHeight = Math.Max(1d, ItemHeight);
        var itemsPerRow = ItemsPerRow;
        var firstVisibleRow = (int)Math.Floor(VerticalOffset / itemHeight);
        var lastVisibleRow = (int)Math.Ceiling((VerticalOffset + ViewportHeight) / itemHeight) - 1;

        // Realize one extra row on each side so fast scrolling never shows blanks.
        firstVisibleRow = Math.Max(0, firstVisibleRow - 1);
        var totalRows = (int)Math.Ceiling((double)itemCount / itemsPerRow) - 1;
        lastVisibleRow = Math.Min(totalRows, lastVisibleRow + 1);

        firstVisibleItemIndex = firstVisibleRow * itemsPerRow;
        lastVisibleItemIndex = Math.Min(itemCount - 1, (lastVisibleRow + 1) * itemsPerRow - 1);
    }

    private void CleanUpItems(int firstChildIndex, int lastChildIndex)
    {
        var children = InternalChildren;
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var childGeneratorPosition = new GeneratorPosition(i, 0);
            var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(childGeneratorPosition);
            if (itemIndex < firstChildIndex || itemIndex > lastChildIndex)
            {
                ItemContainerGenerator.Remove(childGeneratorPosition, 1);
                RemoveInternalChildRange(i, 1);
            }
        }
    }

    private static Size ClampToFinite(Size size) => new(
        double.IsInfinity(size.Width) ? 0 : size.Width,
        double.IsInfinity(size.Height) ? 0 : size.Height);
}
