namespace HanabePhotoManager.Core.Browsing.Treemap;

public enum TreemapWeightMode
{
    FileSize,
    PhotoCount
}

public sealed record TreemapNode
{
    public TreemapNode(string key, string label, double weight, bool isContainer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Key = key;
        Label = label;
        Weight = weight;
        IsContainer = isContainer;
    }

    public string Key { get; }

    public string Label { get; }

    public double Weight { get; }

    public bool IsContainer { get; }
}

public sealed record TreemapBounds
{
    public TreemapBounds(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (!double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        if (!double.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!double.IsFinite(height) || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public double Right => X + Width;

    public double Bottom => Y + Height;
}

public sealed record TreemapTile(TreemapNode Node, TreemapBounds Bounds);

