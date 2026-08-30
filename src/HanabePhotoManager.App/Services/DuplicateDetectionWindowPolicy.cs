using System.Windows;

namespace HanabePhotoManager.App.Services;

internal static class DuplicateDetectionWindowPolicy
{
    internal static bool ShouldRestore(bool enabled, WindowState state, int actionableDuplicateCount) =>
        enabled && state == WindowState.Minimized && actionableDuplicateCount > 0;
}
