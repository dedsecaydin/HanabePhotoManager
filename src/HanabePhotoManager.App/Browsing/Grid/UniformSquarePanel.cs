using System.Windows;
using System.Windows.Controls;
using WpfPanel = System.Windows.Controls.Panel;
using WpfSize = System.Windows.Size;

namespace HanabePhotoManager.App.Browsing.Grid;

/// <summary>
/// Arranges child elements as a uniform grid of squares. All cells have the
/// same <see cref="TileSize"/> and are separated by <see cref="Spacing"/>.
/// The number of columns is derived from the available width, matching the
/// behaviour of a photos app grid that reflows as the viewport or zoom
/// changes.
/// </summary>
public sealed class UniformSquarePanel : WpfPanel
{
    public static readonly DependencyProperty TileSizeProperty = DependencyProperty.Register(
        nameof(TileSize),
        typeof(double),
        typeof(UniformSquarePanel),
        new FrameworkPropertyMetadata(
            150.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(UniformSquarePanel),
        new FrameworkPropertyMetadata(
            14.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double TileSize
    {
        get => (double)GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        var tileSize = Math.Max(1.0, TileSize);
        var spacing = Math.Max(0.0, Spacing);
        var cellStride = tileSize + spacing;

        var columns = ComputeColumns(availableSize.Width, tileSize, spacing);
        var rows = InternalChildren.Count == 0
            ? 0
            : (int)Math.Ceiling(InternalChildren.Count / (double)columns);

        var childConstraint = new WpfSize(tileSize, tileSize);
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(childConstraint);
        }

        var desiredWidth = columns * tileSize + Math.Max(0, columns - 1) * spacing;
        var desiredHeight = rows * tileSize + Math.Max(0, rows - 1) * spacing;
        return new WpfSize(
            double.IsPositiveInfinity(availableSize.Width) ? desiredWidth : Math.Max(0, availableSize.Width),
            double.IsPositiveInfinity(availableSize.Height) ? desiredHeight : Math.Max(0, availableSize.Height));
    }

    protected override WpfSize ArrangeOverride(WpfSize finalSize)
    {
        var tileSize = Math.Max(1.0, TileSize);
        var spacing = Math.Max(0.0, Spacing);
        var columns = ComputeColumns(finalSize.Width, tileSize, spacing);
        var cellStride = tileSize + spacing;

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            var column = index % columns;
            var row = index / columns;
            var x = column * cellStride;
            var y = row * cellStride;
            child.Arrange(new Rect(x, y, tileSize, tileSize));
        }

        return finalSize;
    }

    private static int ComputeColumns(double availableWidth, double tileSize, double spacing)
    {
        if (availableWidth <= 0 || tileSize <= 0)
        {
            return 1;
        }

        var stride = tileSize + spacing;
        var columns = (int)Math.Floor((availableWidth + spacing) / stride);
        return Math.Max(1, columns);
    }
}
