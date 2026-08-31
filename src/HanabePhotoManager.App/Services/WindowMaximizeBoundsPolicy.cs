using System.Drawing;

namespace HanabePhotoManager.App.Services;

internal static class WindowMaximizeBoundsPolicy
{
    internal static Rectangle Calculate(Rectangle monitorBounds, Rectangle workingArea) => new(
        workingArea.Left - monitorBounds.Left,
        workingArea.Top - monitorBounds.Top,
        workingArea.Width,
        workingArea.Height);
}
