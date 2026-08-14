using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HanabePhotoManager.App.Cloud;

/// <summary>
/// Converts a usage fraction (0..1) into the stroked arc geometry of the
/// cloud overview usage ring (128x128 canvas, stroke thickness 12, center at
/// 64,64, radius 58) starting at the top and sweeping clockwise. Values at or
/// below zero produce an empty geometry so unauthenticated / unknown accounts
/// show no fabricated usage.
/// </summary>
public sealed class PercentToArcGeometryConverter : IValueConverter
{
    private const double Center = 64;
    private const double Radius = 58;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = value is double number ? Math.Clamp(number, 0, 1) : 0d;
        if (percent <= 0)
        {
            return Geometry.Empty;
        }

        var angle = percent * 2 * Math.PI;
        var end = new System.Windows.Point(
            Center + Radius * Math.Sin(angle),
            Center - Radius * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new System.Windows.Point(Center, Center - Radius), isFilled: false, isClosed: false);
            context.ArcTo(
                end,
                new System.Windows.Size(Radius, Radius),
                0,
                percent > 0.5,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
