using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App;

public partial class PhotoViewerWindow : Window
{
    private readonly PhotoViewerViewModel _viewModel;
    private bool _isPanning;
    private System.Windows.Point _panStart;
    private double _panHorizontalOffset;
    private double _panVerticalOffset;

    public PhotoViewerWindow(IEnumerable<string> paths, string selectedPath, Action<string>? photoDeleted = null)
    {
        InitializeComponent();
        _viewModel = new PhotoViewerViewModel();
        if (photoDeleted is not null) _viewModel.PhotoDeleted += photoDeleted;
        DataContext = _viewModel;
        _viewModel.Open(paths, selectedPath);
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PhotoViewerViewModel.IsOpen) && !_viewModel.IsOpen) Close();
        };
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Up) { _viewModel.Previous(); e.Handled = true; }
        else if (e.Key is Key.Right or Key.Down) { _viewModel.Next(); e.Handled = true; }
        else if (e.Key == Key.Delete) { ConfirmDeleteCurrent(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key is >= Key.D1 and <= Key.D5) { _viewModel.SetRating((int)e.Key - (int)Key.D0); e.Handled = true; }
        else if (e.Key is >= Key.NumPad1 and <= Key.NumPad5) { _viewModel.SetRating((int)e.Key - (int)Key.NumPad0); e.Handled = true; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Delete_Click(object sender, RoutedEventArgs e) => ConfirmDeleteCurrent();

    private void ConfirmDeleteCurrent()
    {
        var name = _viewModel.Metadata.Name;
        if (!DeleteConfirmationWindow.Confirm(
                this,
                $"确定把 {name} 移入回收站吗？",
                selectedCount: 1,
                actualFileCount: 1))
            return;

        _viewModel.DeleteCurrent();
    }

    private void PhotoViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var pointer = e.GetPosition(PhotoViewport);
        var oldWidth = PhotoViewport.ExtentWidth;
        var oldHeight = PhotoViewport.ExtentHeight;
        var oldHorizontal = PhotoViewport.HorizontalOffset;
        var oldVertical = PhotoViewport.VerticalOffset;
        _viewModel.AdjustZoom(Math.Sign(e.Delta));
        Dispatcher.BeginInvoke(new Action(() =>
        {
            PhotoViewport.UpdateLayout();
            PhotoViewport.ScrollToHorizontalOffset(PhotoViewportMath.AnchoredOffset(
                oldWidth, PhotoViewport.ExtentWidth, oldHorizontal, pointer.X, PhotoViewport.ScrollableWidth));
            PhotoViewport.ScrollToVerticalOffset(PhotoViewportMath.AnchoredOffset(
                oldHeight, PhotoViewport.ExtentHeight, oldVertical, pointer.Y, PhotoViewport.ScrollableHeight));
        }), DispatcherPriority.Loaded);
        e.Handled = true;
    }

    private void PhotoViewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _viewModel.ResetZoom();
            PhotoViewport.ScrollToHome();
            e.Handled = true;
            return;
        }
        _isPanning = true;
        _panStart = e.GetPosition(this);
        _panHorizontalOffset = PhotoViewport.HorizontalOffset;
        _panVerticalOffset = PhotoViewport.VerticalOffset;
        PhotoViewport.Cursor = System.Windows.Input.Cursors.SizeAll;
        PhotoViewport.CaptureMouse();
        e.Handled = true;
    }

    private void PhotoViewport_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isPanning || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(this);
        PhotoViewport.ScrollToHorizontalOffset(PhotoViewportMath.DragOffset(
            _panHorizontalOffset, current.X - _panStart.X, PhotoViewport.ScrollableWidth));
        PhotoViewport.ScrollToVerticalOffset(PhotoViewportMath.DragOffset(
            _panVerticalOffset, current.Y - _panStart.Y, PhotoViewport.ScrollableHeight));
        e.Handled = true;
    }

    private void PhotoViewport_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndPan();
    private void PhotoViewport_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e) => EndPan();

    private void EndPan()
    {
        if (!_isPanning) return;
        _isPanning = false;
        PhotoViewport.ReleaseMouseCapture();
        PhotoViewport.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Button) return;
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
}
