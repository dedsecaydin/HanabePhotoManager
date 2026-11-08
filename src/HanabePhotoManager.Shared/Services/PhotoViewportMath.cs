namespace HanabePhotoManager.App.Services;

/// <summary>提供查看器缩放、平移和保持指针锚点所需的纯坐标计算。</summary>
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
