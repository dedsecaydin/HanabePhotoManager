using System.Windows;

namespace HanabePhotoManager.App.Services;

internal static class HanabeAssistantWindowPolicy
{
    internal static bool ShouldShow(WindowState state, bool enabled) =>
        state == WindowState.Minimized && enabled;
}
