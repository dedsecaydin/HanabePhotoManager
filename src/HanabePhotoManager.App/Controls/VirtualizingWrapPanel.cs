using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Size = System.Windows.Size;
using Point = System.Windows.Point;

namespace HanabePhotoManager.App.Controls;

/// <summary>
/// Marks an item that should occupy an entire row in a
/// <see cref="VirtualizingWrapPanel"/> (e.g. a date-section header between
/// rows of photo tiles). Such items are laid out at full panel width with a
/// fixed <see cref="VirtualizingWrapPanel.HeaderHeight"/> while every other
/// item keeps the uniform tile size.
/// </summary>
public interface IWallSectionHeader
{
}

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
/// Items implementing <see cref="IWallSectionHeader"/> are laid out as full
/// width rows (see <see cref="HeaderHeight"/>) so a flattened photo wall can
/// keep per-date section headers inside the same virtualizing surface.
/// </remarks>
public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    private const double ScrollLineAmount = 16d;

    private Size _extent = new();
    private Size _viewport = new();
    private Point _offset = new();
    private ScrollViewer? _scrollOwner;

    // Row table: one entry per laid-out row (a header row or a row of tiles).
    // Rebuilt on every measure pass (O(n), n = item count, which is cheap
    // compared with realizing containers) so collection reshuffles — e.g. a
    // section collapsing or re-expanding with the same item count — never
    // leave a stale layout behind.
    private List<RowInfo> _rows = [];

    // Tracks the last logical (content-space) top of every realized item, keyed by
    // the item object (stable across flat-list insertions/removals). On a layout
    // change (a date section expanding/collapsing) the row top of items below the
    // change point shifts; we animate their containers' RenderTransform from the
    // old offset back to zero so they slide smoothly instead of jumping.
    private readonly Dictionary<object, double> _itemTops = [];

    /// <summary>
    /// Set to <c>true</c> (by the browse page) only when a date section expands or
    /// collapses, so the layout-transition slide plays for that change. Other
    /// layout changes — Ctrl+wheel zoom, window resize, date/filter switches —
    /// must NOT trigger the slide, otherwise zooming feels janky. Reset by
    /// <see cref="ArrangeOverride"/> after a single pass.
    /// </summary>
    public static bool AnimateLayoutTransition { get; set; }

    private readonly struct RowInfo(int startIndex, int count, bool isHeader)
    {
        public int StartIndex { get; } = startIndex;
        public int Count { get; } = count;
        public bool IsHeader { get; } = isHeader;
    }

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

    /// <summary>
    /// Height reserved for items implementing <see cref="IWallSectionHeader"/>.
    /// Header items are arranged at the full panel width.
    /// </summary>
    public static readonly DependencyProperty HeaderHeightProperty = DependencyProperty.Register(
        nameof(HeaderHeight),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(44d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

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

    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
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
                var top = GetRowTopForItem(itemIndex);
                var height = IsHeaderItem(itemIndex) ? Math.Max(1d, HeaderHeight) : Math.Max(1d, ItemHeight);
                if (top < VerticalOffset)
                {
                    SetVerticalOffset(top);
                }
                else if (top + height > VerticalOffset + ViewportHeight)
                {
                    SetVerticalOffset(top + height - ViewportHeight);
                }

                break;
            }

            element = VisualTreeHelper.GetParent(element) as FrameworkElement;
        }

        return rectangle;
    }

    // ---- Row table ----

    private bool IsHeaderItem(int itemIndex)
    {
        var items = ItemsOwner?.Items;
        return items is not null && itemIndex >= 0 && itemIndex < items.Count && items[itemIndex] is IWallSectionHeader;
    }

    private double RowHeight(RowInfo row) => row.IsHeader ? Math.Max(1d, HeaderHeight) : Math.Max(1d, ItemHeight);

    private void EnsureRowTable()
    {
        var itemCount = ItemCount;
        var viewportWidth = double.IsInfinity(ViewportWidth) || ViewportWidth <= 0
            ? Math.Max(1d, ItemWidth)
            : ViewportWidth;
        _rows = BuildRows(itemCount, viewportWidth);
    }

    private List<RowInfo> BuildRows(int itemCount, double viewportWidth)
    {
        var rows = new List<RowInfo>();
        if (itemCount == 0)
        {
            return rows;
        }

        var itemsPerRow = Math.Max(1, (int)Math.Floor(viewportWidth / Math.Max(1d, ItemWidth)));
        var items = ItemsOwner?.Items;
        var rowStart = 0;
        var rowCount = 0;
        var isHeaderRow = false;

        for (var i = 0; i < itemCount; i++)
        {
            var isHeader = items is not null && items[i] is IWallSectionHeader;
            if (isHeader)
            {
                // Flush the pending tile row (if any) before starting a header row.
                if (rowCount > 0)
                {
                    rows.Add(new RowInfo(rowStart, rowCount, isHeaderRow));
                }

                rows.Add(new RowInfo(i, 1, true));
                rowStart = i + 1;
                rowCount = 0;
                isHeaderRow = false;
            }
            else
            {
                if (rowCount == 0)
                {
                    rowStart = i;
                    isHeaderRow = false;
                }

                rowCount++;
                if (rowCount >= itemsPerRow)
                {
                    rows.Add(new RowInfo(rowStart, rowCount, false));
                    rowStart = i + 1;
                    rowCount = 0;
                }
            }
        }

        if (rowCount > 0)
        {
            rows.Add(new RowInfo(rowStart, rowCount, false));
        }

        return rows;
    }

    private double GetRowTopForItem(int itemIndex)
    {
        EnsureRowTable();
        var y = 0d;
        foreach (var row in _rows)
        {
            if (itemIndex >= row.StartIndex && itemIndex < row.StartIndex + row.Count)
            {
                return y;
            }

            y += RowHeight(row);
        }

        return 0;
    }

    // ---- Measure / Arrange ----

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateScrollInfo(availableSize);
        EnsureRowTable();

        var generator = ItemContainerGenerator;
        var itemCount = ItemCount;
        if (itemCount == 0)
        {
            CleanUpItems(0, -1);
            return _viewport;
        }

        GetVisibleRange(out var firstVisibleItemIndex, out var lastVisibleItemIndex);
        if (firstVisibleItemIndex < 0 || lastVisibleItemIndex < firstVisibleItemIndex)
        {
            CleanUpItems(0, -1);
            return _viewport;
        }

        var startPosition = generator.GeneratorPositionFromIndex(firstVisibleItemIndex);
        var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;
        var viewportWidth = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0
            ? Math.Max(1d, ItemWidth)
            : availableSize.Width;

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

                var measureWidth = IsHeaderItem(itemIndex) ? viewportWidth : Math.Max(1d, ItemWidth);
                var measureHeight = IsHeaderItem(itemIndex) ? Math.Max(1d, HeaderHeight) : Math.Max(1d, ItemHeight);
                child?.Measure(new Size(measureWidth, measureHeight));
            }
        }

        CleanUpItems(firstVisibleItemIndex, lastVisibleItemIndex);
        return _viewport;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureRowTable();

        // Pre-compute the top of every row so children can be placed cheaply.
        var rowTops = new double[_rows.Count];
        var y = 0d;
        for (var r = 0; r < _rows.Count; r++)
        {
            rowTops[r] = y;
            y += RowHeight(_rows[r]);
        }

        var viewportWidth = double.IsInfinity(finalSize.Width) || finalSize.Width <= 0
            ? Math.Max(1d, ItemWidth)
            : finalSize.Width;

        foreach (UIElement child in InternalChildren)
        {
            var itemIndex = ContainerGenerator?.IndexFromContainer(child) ?? -1;
            if (itemIndex < 0)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            var rowIndex = FindRowIndex(itemIndex);
            if (rowIndex < 0)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            var row = _rows[rowIndex];
            var rowTop = rowTops[rowIndex];
            if (row.IsHeader)
            {
                AnimateSlide(child, itemIndex, rowTop);
                child.Arrange(new Rect(
                    -HorizontalOffset,
                    rowTop - VerticalOffset,
                    viewportWidth,
                    Math.Max(1d, HeaderHeight)));
                continue;
            }

            var itemWidth = Math.Max(1d, ItemWidth);
            var itemHeight = Math.Max(1d, ItemHeight);
            var column = itemIndex - row.StartIndex;
            // 强制使用 ItemWidth/ItemHeight（方形 tile），而不是内容自适应高度——
            // 否则缩略图按照片原始比例（3:2 横图）撑开 tile，网格看起来"扁"。
            AnimateSlide(child, itemIndex, rowTop);
            child.Arrange(new Rect(
                column * itemWidth - HorizontalOffset,
                rowTop - VerticalOffset,
                itemWidth,
                itemHeight));
        }

        AnimateLayoutTransition = false;
        return finalSize;
    }

    private int FindRowIndex(int itemIndex)
    {
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            if (itemIndex >= row.StartIndex && itemIndex < row.StartIndex + row.Count)
            {
                return r;
            }
        }

        return -1;
    }

    /// <summary>
    /// Smoothly slides a realized item from its previous logical top to its new
    /// top when a date section expands/collapses and pushes the rows below. The
    /// item is already arranged at its new position; a transient
    /// <see cref="TranslateTransform"/> offsets it back to the old position and
    /// eases to zero. Scrolling does not change the logical top, so this never
    /// fires during a normal scroll.
    /// </summary>
    private void AnimateSlide(UIElement child, int itemIndex, double rowTop)
    {
        var items = ItemsOwner?.Items;
        if (items is null || itemIndex < 0 || itemIndex >= items.Count)
        {
            return;
        }

        var itemObject = items[itemIndex];
        var hasOldTop = _itemTops.TryGetValue(itemObject, out var oldTop);
        var moved = AnimateLayoutTransition && hasOldTop && Math.Abs(oldTop - rowTop) > 0.5;
        _itemTops[itemObject] = rowTop;

        if (!moved)
        {
            return;
        }

        var delta = oldTop - rowTop;
        var translate = new TranslateTransform(0, delta);
        child.RenderTransform = translate;
        var animation = new DoubleAnimation(delta, 0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        translate.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    /// <summary>
    /// Returns the laid-out bounds of every item — including items that are not
    /// currently realized — in panel coordinates (already offset by the current
    /// scroll position, i.e. viewport-relative). Consumers such as the browse
    /// page's rubber-band selection use this to hit-test against the whole
    /// virtualized wall instead of only the realized window that exists in the
    /// visual tree. Header items report <paramref name="isHeader"/> as
    /// <c>true</c> so callers can skip them.
    /// </summary>
    public IReadOnlyList<(int ItemIndex, Rect Bounds, bool IsHeader)> GetItemBounds()
    {
        EnsureRowTable();
        var result = new List<(int, Rect, bool)>(_rows.Count * 2);
        if (_rows.Count == 0)
        {
            return result;
        }

        var viewportWidth = double.IsInfinity(ViewportWidth) || ViewportWidth <= 0
            ? Math.Max(1d, ItemWidth)
            : ViewportWidth;
        var itemWidth = Math.Max(1d, ItemWidth);
        var itemHeight = Math.Max(1d, ItemHeight);
        var headerHeight = Math.Max(1d, HeaderHeight);

        var rowTops = new double[_rows.Count];
        var y = 0d;
        for (var r = 0; r < _rows.Count; r++)
        {
            rowTops[r] = y;
            y += RowHeight(_rows[r]);
        }

        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            var top = rowTops[r] - VerticalOffset;
            if (row.IsHeader)
            {
                result.Add((row.StartIndex, new Rect(-HorizontalOffset, top, viewportWidth, headerHeight), true));
            }
            else
            {
                for (var c = 0; c < row.Count; c++)
                {
                    result.Add((
                        row.StartIndex + c,
                        new Rect(c * itemWidth - HorizontalOffset, top, itemWidth, itemHeight),
                        false));
                }
            }
        }

        return result;
    }

    private void UpdateScrollInfo(Size availableSize)
    {
        var itemCount = ItemCount;
        var extent = CalculateExtent(availableSize, itemCount);
        if (extent != _extent)
        {
            _extent = extent;
            _scrollOwner?.InvalidateScrollInfo();
        }

        // 虚拟化面板在 CanContentScroll=True 下会被以「高度无穷大」测量；
        // 真正的视口高度取自 ScrollOwner（ScrollViewer）。否则 ViewportHeight 会变成
        // 无穷大，导致 ExtentHeight 与滚动全部失效。
        var viewportWidth = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0
            ? Math.Max(1d, ItemWidth)
            : availableSize.Width;
        var viewportHeight = double.IsInfinity(availableSize.Height) || availableSize.Height <= 0
            ? Math.Max(1d, _scrollOwner?.ViewportHeight ?? ItemHeight)
            : availableSize.Height;
        var viewport = new Size(viewportWidth, viewportHeight);
        if (viewport != _viewport)
        {
            _viewport = viewport;
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

        EnsureRowTable();
        var totalHeight = 0d;
        foreach (var row in _rows)
        {
            totalHeight += RowHeight(row);
        }

        return new Size(viewportWidth, Math.Max(totalHeight, viewportHeight));
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

        EnsureRowTable();

        // Find the first row whose bottom edge is below the scroll offset.
        var firstRow = -1;
        var y = 0d;
        for (var r = 0; r < _rows.Count; r++)
        {
            var h = RowHeight(_rows[r]);
            if (y + h > VerticalOffset)
            {
                firstRow = r;
                break;
            }

            y += h;
        }

        if (firstRow < 0)
        {
            firstRow = _rows.Count - 1;
        }

        // Realize one extra row above so fast scrolling never shows blanks.
        firstRow = Math.Max(0, firstRow - 1);

        // Find the last row that starts before the bottom of the viewport.
        var lastRow = firstRow;
        y = 0d;
        for (var r = 0; r < _rows.Count; r++)
        {
            var h = RowHeight(_rows[r]);
            if (r >= firstRow && y <= VerticalOffset + ViewportHeight)
            {
                lastRow = r;
            }

            y += h;
        }

        // Realize one extra row below.
        lastRow = Math.Min(_rows.Count - 1, lastRow + 1);

        firstVisibleItemIndex = _rows[firstRow].StartIndex;
        lastVisibleItemIndex = Math.Min(itemCount - 1, _rows[lastRow].StartIndex + _rows[lastRow].Count - 1);
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

}
