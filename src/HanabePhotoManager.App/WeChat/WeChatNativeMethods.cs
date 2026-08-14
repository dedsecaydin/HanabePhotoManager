using System.Runtime.InteropServices;

namespace HanabePhotoManager.App.WeChat;

internal static class WeChatNativeMethods
{
    internal const int SwRestore = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);
}

public static class WeChatForegroundVerifier
{
    public static bool IsVerifiedForeground(
        int foregroundProcessId,
        IReadOnlyCollection<int> verifiedProcessIds) =>
        foregroundProcessId > 0 && verifiedProcessIds.Contains(foregroundProcessId);
}
