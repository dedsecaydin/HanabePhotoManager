namespace HanabePhotoManager.Core.Browsing.Treemap;

/// <summary>
/// 等高填充相册布局中的单个矩形结果。
/// </summary>
public sealed record JustifiedItem(double X, double Y, double Width, double Height, double AspectRatio);

/// <summary>
/// 将已知宽高比的媒体排列为填满可用宽度的等高行。
/// </summary>
public sealed class JustifiedGalleryLayout
{
    private readonly double _minAspect;
    private readonly double _maxAspect;
    private readonly double _gap;
    private readonly double _targetRowHeight;
    private readonly double _minRowFill;

    /// <param name="targetRowHeight">首选行高（像素）。</param>
    /// <param name="minAspect">单项允许的最小宽高比。</param>
    /// <param name="maxAspect">单项允许的最大宽高比。</param>
    /// <param name="gap">同一行项目之间的像素间距。</param>
    /// <param name="minRowFill">
    /// 最后一行允许拉伸前必须达到的容器宽度比例（0–1）；不足时保留自然宽度。</param>
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
    /// 在指定容器宽度内计算等高填充矩形。结果从局部 Y=0 开始，调用方负责叠加容器偏移。
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

            // 以“现在结束本行”反推行高，避免先固定项目数再产生大面积空白。
            var rowAspectSum = RowAspectSum(row);
            var availableWidth = containerWidth - _gap * (row.Count - 1);
            var height = availableWidth / rowAspectSum;

            // 行高进入可接受区间或到达末项时结束当前行。
            var isLast = i == items.Count - 1;
            var acceptableHeight = height <= _targetRowHeight * 1.25;
            if (acceptableHeight || isLast)
            {
                // 最后一行过稀时保持目标行高，不为填满宽度而夸张放大少量照片。
                if (isLast && !acceptableHeight)
                {
                    var naturalWidth = rowAspectSum * _targetRowHeight + _gap * (row.Count - 1);
                    var fillRatio = naturalWidth / containerWidth;
                    if (fillRatio < _minRowFill)
                    {
                        height = _targetRowHeight;
                    }
                }

                // 防止极端宽高比把行高压缩或放大到不可辨认范围。
                height = Math.Clamp(height, _targetRowHeight * 0.4, _targetRowHeight * 2.5);

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
