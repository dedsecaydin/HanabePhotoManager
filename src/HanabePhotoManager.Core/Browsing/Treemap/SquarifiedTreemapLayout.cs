namespace HanabePhotoManager.Core.Browsing.Treemap;

public sealed class SquarifiedTreemapLayout
{
    public IReadOnlyList<TreemapTile> Calculate(
        IReadOnlyList<TreemapNode> nodes,
        TreemapBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(bounds);

        var ordered = nodes
            .Where(node => double.IsFinite(node.Weight) && node.Weight > 0)
            .OrderByDescending(node => node.Weight)
            .ThenBy(node => node.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }

        var totalWeight = ordered.Sum(node => node.Weight);
        var scale = bounds.Width * bounds.Height / totalWeight;
        var remaining = new MutableBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var pending = ordered
            .Select(node => new WeightedNode(node, node.Weight * scale))
            .ToList();
        var row = new List<WeightedNode>();
        var result = new List<TreemapTile>(ordered.Length);

        while (pending.Count > 0)
        {
            var candidate = pending[0];
            var side = Math.Min(remaining.Width, remaining.Height);
            if (row.Count == 0 ||
                WorstAspect(row.Append(candidate), side) <= WorstAspect(row, side))
            {
                row.Add(candidate);
                pending.RemoveAt(0);
                continue;
            }

            LayoutRow(row, remaining, result);
            row.Clear();
        }

        if (row.Count > 0)
        {
            LayoutRow(row, remaining, result);
        }

        return result;
    }

    private static double WorstAspect(IEnumerable<WeightedNode> row, double side)
    {
        var values = row.Select(item => item.Area).ToArray();
        if (values.Length == 0 || side <= 0)
        {
            return double.PositiveInfinity;
        }

        var sum = values.Sum();
        var sideSquared = side * side;
        var sumSquared = sum * sum;
        return Math.Max(
            sideSquared * values.Max() / sumSquared,
            sumSquared / (sideSquared * values.Min()));
    }

    private static void LayoutRow(
        IReadOnlyList<WeightedNode> row,
        MutableBounds remaining,
        ICollection<TreemapTile> result)
    {
        var rowArea = row.Sum(item => item.Area);
        if (remaining.Width >= remaining.Height)
        {
            var rowWidth = Math.Min(remaining.Width, rowArea / remaining.Height);
            var y = remaining.Y;
            for (var index = 0; index < row.Count; index++)
            {
                var height = index == row.Count - 1
                    ? remaining.Bottom - y
                    : row[index].Area / rowWidth;
                result.Add(new TreemapTile(
                    row[index].Node,
                    new TreemapBounds(remaining.X, y, rowWidth, height)));
                y += height;
            }

            remaining.X += rowWidth;
            remaining.Width -= rowWidth;
        }
        else
        {
            var rowHeight = Math.Min(remaining.Height, rowArea / remaining.Width);
            var x = remaining.X;
            for (var index = 0; index < row.Count; index++)
            {
                var width = index == row.Count - 1
                    ? remaining.Right - x
                    : row[index].Area / rowHeight;
                result.Add(new TreemapTile(
                    row[index].Node,
                    new TreemapBounds(x, remaining.Y, width, rowHeight)));
                x += width;
            }

            remaining.Y += rowHeight;
            remaining.Height -= rowHeight;
        }
    }

    private sealed record WeightedNode(TreemapNode Node, double Area);

    private sealed class MutableBounds(double x, double y, double width, double height)
    {
        public double X { get; set; } = x;

        public double Y { get; set; } = y;

        public double Width { get; set; } = width;

        public double Height { get; set; } = height;

        public double Right => X + Width;

        public double Bottom => Y + Height;
    }
}
