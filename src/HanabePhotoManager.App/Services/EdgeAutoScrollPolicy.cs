using System.Windows;

namespace HanabePhotoManager.App.Services;

public sealed class EdgeAutoScrollPolicy(double maximumDelta = 22)
{
    public Vector Calculate(System.Windows.Point pointer, System.Windows.Size viewport, double edge = 48)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0 || edge <= 0) return new Vector();
        return new Vector(
            CalculateAxis(pointer.X, viewport.Width, edge),
            CalculateAxis(pointer.Y, viewport.Height, edge));
    }

    private double CalculateAxis(double position, double length, double edge)
    {
        if (position < edge)
        {
            return -maximumDelta * Math.Clamp((edge - position) / edge, 0, 1);
        }

        if (position > length - edge)
        {
            return maximumDelta * Math.Clamp((position - (length - edge)) / edge, 0, 1);
        }

        return 0;
    }
}
