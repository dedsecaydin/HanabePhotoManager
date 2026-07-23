using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HanabePhotoManager.App.Watermark;

public partial class WatermarkPage : System.Windows.Controls.UserControl
{
    private bool _isDraggingWatermark;

    public WatermarkPage() => InitializeComponent();
    private WatermarkViewModel? ViewModel => DataContext as WatermarkViewModel;
    private void Page_DragOver(object sender, System.Windows.DragEventArgs e) { e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None; e.Handled = true; }
    private void Page_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (ViewModel is null || e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths) return;
        var png = paths.Length == 1 && string.Equals(System.IO.Path.GetExtension(paths[0]), ".png", StringComparison.OrdinalIgnoreCase);
        if (png && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ViewModel.SetWatermark(paths[0]); else ViewModel.AddInputs(paths);
    }
    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingWatermark = true;
        PreviewSurface.CaptureMouse();
        UpdateWatermarkPosition(e);
    }

    private void Preview_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDraggingWatermark && e.LeftButton == MouseButtonState.Pressed) UpdateWatermarkPosition(e);
    }

    private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingWatermark) return;
        UpdateWatermarkPosition(e);
        _isDraggingWatermark = false;
        PreviewSurface.ReleaseMouseCapture();
    }

    private void Watermark_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Watermark_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (ViewModel is null || e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] { Length: > 0 } paths) return;
        ViewModel.SetWatermark(paths[0]);
        e.Handled = true;
    }

    private void UpdateWatermarkPosition(System.Windows.Input.MouseEventArgs e)
    {
        if (ViewModel is null || PreviewSurface.ActualWidth <= 0 || PreviewSurface.ActualHeight <= 0) return;
        var point = e.GetPosition(PreviewSurface);
        ViewModel.SetNormalizedPosition(point.X / PreviewSurface.ActualWidth, point.Y / PreviewSurface.ActualHeight);
    }
}
