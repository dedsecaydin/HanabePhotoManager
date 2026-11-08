using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace HanabePhotoManager.App;

/// <summary>
/// 查看器窗口大小：无边窗口，按所在显示器的可用区域取 86% × 88%（不小于 980×640），
/// 居中在主窗口所在的显示器上；Windows 11 上用系统圆角。F11 / 双击空白区切换铺满屏幕。
/// </summary>
public partial class PhotoViewerWindow
{
    internal const double WidthRatio = 0.86;
    internal const double HeightRatio = 0.88;

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var anchor = Owner is { } owner ? new WindowInteropHelper(owner).Handle : handle;
        var work = WorkAreaInDips(anchor, handle);
        var size = ViewerWindowSize(work.Width, work.Height, MinWidth, MinHeight);
        Width = size.Width;
        Height = size.Height;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;

        // Windows 11：无边窗口也用系统圆角（旧系统忽略这个属性）
        var preference = DwmRoundCorners;
        _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref preference, sizeof(int));
    }

    /// <summary>可用区域里的窗口大小：按比例取，但不小于最小尺寸、不超过可用区域。</summary>
    internal static Size ViewerWindowSize(double workWidth, double workHeight, double minWidth, double minHeight) =>
        new(Math.Min(workWidth, Math.Max(minWidth, Math.Round(workWidth * WidthRatio))),
            Math.Min(workHeight, Math.Max(minHeight, Math.Round(workHeight * HeightRatio))));

    private Rect WorkAreaInDips(IntPtr anchor, IntPtr self)
    {
        var monitor = MonitorFromWindow(anchor, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return SystemParameters.WorkArea;
        var source = HwndSource.FromHwnd(self);
        var toDip = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = toDip.Transform(new Point(info.Work.Left, info.Work.Top));
        var bottomRight = toDip.Transform(new Point(info.Work.Right, info.Work.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private const int MonitorDefaultToNearest = 2;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmRoundCorners = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
