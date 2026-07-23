namespace HanabePhotoManager.App.Services;

public static class PhotoViewportMath
{
    public static double AnchoredOffset(
        double oldExtent, double newExtent, double oldOffset, double pointerPosition, double maximumOffset)
    {
        if (oldExtent <= 0 || newExtent <= 0) return 0;
        var anchor = Math.Clamp((oldOffset + pointerPosition) / oldExtent, 0, 1);
        return Math.Clamp(anchor * newExtent - pointerPosition, 0, Math.Max(0, maximumOffset));
    }

    public static double DragOffset(double startOffset, double pointerDelta, double maximumOffset) =>
        Math.Clamp(startOffset - pointerDelta, 0, Math.Max(0, maximumOffset));
}
