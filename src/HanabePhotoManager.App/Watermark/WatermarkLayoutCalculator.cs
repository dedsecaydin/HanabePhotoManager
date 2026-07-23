namespace HanabePhotoManager.App.Watermark;

public sealed record WatermarkLayoutSettings(double CenterX, double CenterY, double WidthRatio, double Opacity);
public sealed record WatermarkTileSettings(bool Automatic, double Density, double HorizontalGapRatio, double VerticalGapRatio, double RotationDegrees, bool Stagger, double Opacity, double WidthRatio = 0.16);
public sealed record WatermarkPlacement(int X, int Y, int Width, int Height, double RotationDegrees, double Opacity);

public static class WatermarkLayoutCalculator
{
    public static WatermarkPlacement CalculateSingle(int imageWidth, int imageHeight, int markWidth, int markHeight, WatermarkLayoutSettings settings)
    {
        Validate(imageWidth, imageHeight, markWidth, markHeight);
        var ratio = Math.Clamp(settings.WidthRatio, 0.02, 1);
        var width = Math.Min(imageWidth, Math.Max(1, (int)Math.Round(imageWidth * ratio)));
        var height = Math.Min(imageHeight, Math.Max(1, (int)Math.Round(width * markHeight / (double)markWidth)));
        var x = (int)Math.Round(Math.Clamp(settings.CenterX, 0, 1) * imageWidth - width / 2d);
        var y = (int)Math.Round(Math.Clamp(settings.CenterY, 0, 1) * imageHeight - height / 2d);
        return new(Math.Clamp(x, 0, imageWidth - width), Math.Clamp(y, 0, imageHeight - height), width, height, 0, Math.Clamp(settings.Opacity, 0, 1));
    }

    public static IReadOnlyList<WatermarkPlacement> CalculateTiled(int imageWidth, int imageHeight, int markWidth, int markHeight, WatermarkTileSettings settings)
    {
        Validate(imageWidth, imageHeight, markWidth, markHeight);
        var width = Math.Max(1, (int)Math.Round(imageWidth * Math.Clamp(settings.WidthRatio, .03, .6)));
        var height = Math.Max(1, (int)Math.Round(width * markHeight / (double)markWidth));
        double hGap = settings.Automatic ? .42 - .34 * Math.Clamp(settings.Density, 0, 1) : Math.Clamp(settings.HorizontalGapRatio, 0, 1);
        double vGap = settings.Automatic ? .38 - .30 * Math.Clamp(settings.Density, 0, 1) : Math.Clamp(settings.VerticalGapRatio, 0, 1);
        var stepX = Math.Max(1, (int)Math.Round(width * (1 + hGap)));
        var stepY = Math.Max(1, (int)Math.Round(height * (1 + vGap)));
        var angle = settings.Automatic ? -24 : Math.Clamp(settings.RotationDegrees, -90, 90);
        var stagger = settings.Automatic || settings.Stagger;
        var result = new List<WatermarkPlacement>();
        var row = 0;
        for (var y = -height; y < imageHeight + height; y += stepY, row++)
            for (var x = -width + (stagger && row % 2 != 0 ? stepX / 2 : 0); x < imageWidth + width; x += stepX)
                result.Add(new(x, y, width, height, angle, Math.Clamp(settings.Opacity, 0, 1)));
        return result;
    }

    private static void Validate(params int[] values)
    {
        if (values.Any(value => value <= 0)) throw new ArgumentOutOfRangeException(nameof(values));
    }
}
