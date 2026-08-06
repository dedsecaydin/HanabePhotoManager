namespace HanabePhotoManager.Core.Browsing.Treemap;

/// <summary>
/// Result from a justified gallery row layout.
/// </summary>
public sealed record JustifiedItem(double X, double Y, double Width, double Height, double AspectRatio);

/// <summary>
/// Arranges items with known aspect ratios into rows that fill the available
/// width, like a photo-wall / Flickr justified view.
/// </summary>
public sealed class JustifiedGalleryLayout
{
    private readonly double _minAspect;
    private readonly double _maxAspect;
    private readonly double _gap;
    private readonly double _targetRowHeight;
    private readonly double _minRowFill;

    /// <param name="targetRowHeight">Preferred row height in pixels.</param>
    /// <param name="minAspect">Clamp each item's aspect ratio to at least this.</param>
    /// <param name="maxAspect">Clamp each item's aspect ratio to at most this.</param>
    /// <param name="gap">Gap between items in each row, in pixels.</param>
    /// <param name="minRowFill">
    /// Minimum fraction (0–1) of the container width the last row must reach
    /// to be stretched. Below this, the row keeps its natural width.</param>
    public JustifiedGalleryLayout(
        double targetRowHeight = 180,
        double minAspect = 0.35,
        double maxAspect = 3.5,
        double gap = 1,
        double minRowFill = 0.70)
    {
        _targetRowHeight = Math.Max(20, targetRowHeight);
        _minAspect = Math.Max(0.1, minAspect);
        _maxAspect = Math.Max(_minAspect, maxAspect);
        _gap = Math.Max(0, gap);
        _minRowFill = Math.Clamp(minRowFill, 0, 1);
    }

    /// <summary>
    /// Compute justified-gallery rectangles inside <paramref name="containerWidth"/>.
    /// Items are placed top-to-bottom. Callers must add the container Y offset.
    /// </summary>
    public IReadOnlyList<JustifiedItem> Arrange(
        IReadOnlyList<(double aspectRatio, string? key)> items,
        double containerWidth)
    {
        if (items.Count == 0 || containerWidth <= 0)
        {
            return Array.Empty<JustifiedItem>();
        }

        var result = new List<JustifiedItem>(items.Count);
        var row = new List<(double aspect, string? key)>();
        var y = 0.0;

        for (var i = 0; i < items.Count; i++)
        {
            var aspect = ClampAspect(items[i].aspectRatio);
            row.Add((aspect, items[i].key));

            // Compute what row height would be if we stopped here
            var rowAspectSum = RowAspectSum(row);
            var availableWidth = containerWidth - _gap * (row.Count - 1);
            var height = availableWidth / rowAspectSum;

            // If this height would be acceptable OR it's the last item, finalize the row
            var isLast = i == items.Count - 1;
            var acceptableHeight = height <= _targetRowHeight * 1.25;
            if (acceptableHeight || isLast)
            {
                // For last row, check fill percentage
                if (isLast && !acceptableHeight)
                {
                    // Calculate how full this row would be if we stretch
                    var naturalWidth = rowAspectSum * _targetRowHeight + _gap * (row.Count - 1);
                    var fillRatio = naturalWidth / containerWidth;
                    if (fillRatio < _minRowFill)
                    {
                        // Last row is too sparse — keep natural height, items are small
                        height = _targetRowHeight;
                    }
                }

                // Clamp row height to reasonable range
                height = Math.Max(_targetRowHeight * 0.4, Math.Min(height, _targetRowHeight * 2.5));

                // Place items in this row
                var x = 0.0;
                foreach (var (itemAspect, _) in row)
                {
                    var itemWidth = height * itemAspect;
                    result.Add(new JustifiedItem(x, y, itemWidth, height, itemAspect));
                    x += itemWidth + _gap;
                }

                y += height + _gap;
                row.Clear();
            }
        }

        return result;
    }

    private double ClampAspect(double aspect)
    {
        if (!double.IsFinite(aspect) || aspect <= 0)
        {
            return 1.0;
        }

        return Math.Clamp(aspect, _minAspect, _maxAspect);
    }

    private static double RowAspectSum(List<(double aspect, string? key)> row)
    {
        var sum = 0.0;
        foreach (var (aspect, _) in row)
        {
            sum += aspect;
        }

        return sum;
    }
}