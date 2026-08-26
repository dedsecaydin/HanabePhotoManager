using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;

namespace HanabePhotoManager.App.Watermark;

/// <summary>
/// 水印编辑视图，负责文件拖放、预览画布定位和导出对话框。
/// </summary>
public partial class WatermarkPage : System.Windows.Controls.UserControl
{
    private bool _isDraggingWatermark;
    private WatermarkViewModel? _observedViewModel;

    public WatermarkPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ObserveViewModel();
        Loaded += (_, _) => ObserveViewModel();
        Unloaded += (_, _) => StopObservingViewModel();
    }

    private WatermarkViewModel? ViewModel => DataContext as WatermarkViewModel;

    private void Page_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Page_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (ViewModel is null ||
            e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        // Shift+拖入单个 PNG 表示更换水印；其余拖入均作为待处理图片加入。
        var isSinglePng = paths.Length == 1 &&
            string.Equals(System.IO.Path.GetExtension(paths[0]), ".png", StringComparison.OrdinalIgnoreCase);
        if (isSinglePng && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            ViewModel.SetWatermark(paths[0]);
        }
        else
        {
            ViewModel.AddInputs(paths);
        }
    }

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingWatermark = true;
        PreviewSurface.CaptureMouse();
        UpdateWatermarkPosition(e);
    }

    private void Preview_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDraggingWatermark && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateWatermarkPosition(e);
        }
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
        var viewport = CalculateImageViewport();
        if (viewport.IsEmpty) return;
        ViewModel.SetNormalizedPosition(
            (point.X - viewport.X) / viewport.Width,
            (point.Y - viewport.Y) / viewport.Height);
        UpdateSignaturePreviewPosition();
    }

    private void PreviewSurface_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateSignaturePreviewPosition();

    private void ObserveViewModel()
    {
        if (ReferenceEquals(_observedViewModel, ViewModel))
        {
            UpdateSignaturePreviewPosition();
            return;
        }

        StopObservingViewModel();
        _observedViewModel = ViewModel;
        if (_observedViewModel is not null)
            _observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateSignaturePreviewPosition();
    }

    private void StopObservingViewModel()
    {
        if (_observedViewModel is not null)
            _observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _observedViewModel = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WatermarkViewModel.PreviewImage)
            or nameof(WatermarkViewModel.WatermarkPreviewImage)
            or nameof(WatermarkViewModel.PreviewCenterX)
            or nameof(WatermarkViewModel.PreviewCenterY)
            or nameof(WatermarkViewModel.PreviewWatermarkWidth))
        {
            Dispatcher.BeginInvoke(UpdateSignaturePreviewPosition);
        }
    }

    private Rect CalculateImageViewport()
    {
        var image = ViewModel?.PreviewImage;
        if (image is null || image.PixelWidth <= 0 || image.PixelHeight <= 0 ||
            PreviewSurface.ActualWidth <= 0 || PreviewSurface.ActualHeight <= 0)
            return Rect.Empty;

        var scale = Math.Min(
            PreviewSurface.ActualWidth / image.PixelWidth,
            PreviewSurface.ActualHeight / image.PixelHeight);
        var width = image.PixelWidth * scale;
        var height = image.PixelHeight * scale;
        return new Rect(
            (PreviewSurface.ActualWidth - width) / 2,
            (PreviewSurface.ActualHeight - height) / 2,
            width,
            height);
    }

    private void UpdateSignaturePreviewPosition()
    {
        var viewModel = ViewModel;
        var watermark = viewModel?.WatermarkPreviewImage;
        var viewport = CalculateImageViewport();
        if (viewModel is null || watermark is null || viewport.IsEmpty || watermark.PixelWidth <= 0)
            return;

        var width = viewport.Width * Math.Clamp(
            viewModel.SelectedItem?.UseIndividualSettings == true ? viewModel.SelectedItem.SizeRatio : viewModel.SizeRatio,
            .03,
            .6);
        var height = width * watermark.PixelHeight / watermark.PixelWidth;
        if (height > viewport.Height)
        {
            var scale = viewport.Height / height;
            width *= scale;
            height = viewport.Height;
        }
        SignaturePreview.Width = width;
        SignaturePreview.Height = height;
        Canvas.SetLeft(SignaturePreview, Math.Clamp(
            viewport.X + (viewModel.PreviewCenterX * viewport.Width) - (width / 2),
            viewport.Left,
            viewport.Right - width));
        Canvas.SetTop(SignaturePreview, Math.Clamp(
            viewport.Y + (viewModel.PreviewCenterY * viewport.Height) - (height / 2),
            viewport.Top,
            viewport.Bottom - height));
    }
}
