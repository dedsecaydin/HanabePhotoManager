using Point = System.Windows.Point;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HanabePhotoManager.App.Splitting;

public partial class ImageSplitWindow : Window
{
    private readonly ImageSplitViewModel _model = new();
    private Point? _drag;
    public ImageSplitWindow() : this(null) { }
    internal ImageSplitWindow(ResourceDictionary? resources)
    {
        if (resources is not null) Resources.MergedDictionaries.Add(resources);
        InitializeComponent(); DataContext = _model;
        _model.PropertyChanged += ModelChanged;
        Closed += (_, _) => _model.PropertyChanged -= ModelChanged;
        Closing += (_, e) => { if (_model.IsBusy) { _model.CancelCommand.Execute(null); e.Cancel = true; } };
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageSplitViewModel.Crop) or nameof(ImageSplitViewModel.Preview)) DrawPreview();
    }
    private async void ChooseImage(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff|所有文件|*.*" };
        if (picker.ShowDialog(this) == true) await _model.LoadAsync(picker.FileName);
    }
    private void ChooseOutput(object sender, RoutedEventArgs e)
    {
        using var picker = new System.Windows.Forms.FolderBrowserDialog { InitialDirectory = _model.OutputDirectory, Description = "选择 D 盘内的切图输出目录" };
        if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) _model.OutputDirectory = picker.SelectedPath;
    }
    private void OpenResult(object sender, RoutedEventArgs e)
    {
        try { if (Directory.Exists(_model.ResultDirectory)) Process.Start(new ProcessStartInfo(_model.ResultDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { _model.Status = "无法打开文件夹：" + ex.Message; }
    }
    private void DrawPreview()
    {
        PreviewCanvas.Children.Clear();
        if (_model.Preview is not { } bitmap) return;
        double w = bitmap.PixelWidth, h = bitmap.PixelHeight;
        PreviewCanvas.Width = w; PreviewCanvas.Height = h;
        PreviewCanvas.Children.Add(new System.Windows.Controls.Image { Source = bitmap, Width = w, Height = h });
        double sx = w / _model.ImageWidth, sy = h / _model.ImageHeight;
        var crop = _model.Crop;
        double x = crop.X * sx, y = crop.Y * sy, cw = crop.Width * sx, ch = crop.Height * sy;
        void Mask(double left, double top, double width, double height)
        {
            var rect = new System.Windows.Shapes.Rectangle { Width = Math.Max(0, width), Height = Math.Max(0, height), IsHitTestVisible = false };
            rect.SetResourceReference(Shape.FillProperty, "Brush.Overlay.Scrim"); Canvas.SetLeft(rect, left); Canvas.SetTop(rect, top); PreviewCanvas.Children.Add(rect);
        }
        Mask(0, 0, w, y); Mask(0, y + ch, w, h - y - ch); Mask(0, y, x, ch); Mask(x + cw, y, w - x - cw, ch);
        double stroke = Math.Max(w, h) / 700;
        void Rule(double x1, double y1, double x2, double y2)
        {
            foreach (var layer in new[] { ("Brush.Viewer.Chrome", 4.0), ("Brush.Viewer.Text", 1.5) })
            {
                var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, StrokeThickness = stroke * layer.Item2, IsHitTestVisible = false };
                line.SetResourceReference(Shape.StrokeProperty, layer.Item1); PreviewCanvas.Children.Add(line);
            }
        }
        for (int col = 0; col <= _model.Mode.Columns; col++)
        { double xx = x + (col * crop.Width / _model.Mode.Columns) * sx; Rule(xx, y, xx, y + ch); }
        for (int row = 0; row <= _model.Mode.Rows; row++)
        { double yy = y + (row * crop.Height / _model.Mode.Rows) * sy; Rule(x, yy, x + cw, yy); }
        for (int row = 0; row < _model.Mode.Rows; row++)
            for (int col = 0; col < _model.Mode.Columns; col++)
            {
                var text = new TextBlock { Text = $"{row * _model.Mode.Columns + col + 1:00}", FontSize = 16 * stroke, Padding = new Thickness(4 * stroke), IsHitTestVisible = false };
                text.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Viewer.Text"); text.SetResourceReference(TextBlock.BackgroundProperty, "Brush.Viewer.Chrome");
                Canvas.SetLeft(text, x + col * cw / _model.Mode.Columns + 6 * stroke); Canvas.SetTop(text, y + row * ch / _model.Mode.Rows + 6 * stroke); PreviewCanvas.Children.Add(text);
            }
    }
    private void DragStart(object sender, MouseButtonEventArgs e)
    {
        if (!_model.CanExport) return;
        PreviewCanvas.Focus(); _drag = e.GetPosition(PreviewCanvas); PreviewCanvas.CaptureMouse(); e.Handled = true;
    }
    private void DragMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_drag is not { } previous || !_model.CanEdit) return;
        var point = e.GetPosition(PreviewCanvas); var crop = _model.Crop;
        double availableX = (_model.ImageWidth - crop.Width) * PreviewCanvas.Width / _model.ImageWidth;
        double availableY = (_model.ImageHeight - crop.Height) * PreviewCanvas.Height / _model.ImageHeight;
        if (availableX > 0) _model.PositionX = Math.Clamp(_model.PositionX + (point.X - previous.X) * 100 / availableX, 0, 100);
        if (availableY > 0) _model.PositionY = Math.Clamp(_model.PositionY + (point.Y - previous.Y) * 100 / availableY, 0, 100);
        _drag = point;
    }
    private void DragEnd(object sender, MouseButtonEventArgs e) { _drag = null; PreviewCanvas.ReleaseMouseCapture(); }
    private void DragLost(object sender, System.Windows.Input.MouseEventArgs e) => _drag = null;
    private void MoveByKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_model.CanExport) return;
        switch (e.Key)
        {
            case Key.Left: _model.PositionX = Math.Max(0, _model.PositionX - 1); break;
            case Key.Right: _model.PositionX = Math.Min(100, _model.PositionX + 1); break;
            case Key.Up: _model.PositionY = Math.Max(0, _model.PositionY - 1); break;
            case Key.Down: _model.PositionY = Math.Min(100, _model.PositionY + 1); break;
            default: return;
        }
        e.Handled = true;
    }
}

