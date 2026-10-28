using Rectangle = SixLabors.ImageSharp.Rectangle;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Splitting;

public sealed record SplitMode(string Label, int Columns, int Rows);

public partial class ImageSplitViewModel : ObservableObject
{
    public IReadOnlyList<SplitMode> Modes { get; } = [new("中分 · 左右两张", 2, 1), new("中分 · 上下两张", 1, 2), new("六分 · 两行三列", 3, 2), new("六分 · 三行两列", 2, 3), new("九宫格 · 三行三列", 3, 3)];
    [ObservableProperty] private SplitMode _mode = new("九宫格 · 三行三列", 3, 3);
    [ObservableProperty] private BitmapSource? _preview;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "选择图片，拖动选区调整裁切位置。";
    [ObservableProperty] private string _sourcePath = "";
    [ObservableProperty] private string _outputDirectory = Path.Combine(AppDataPaths.ProcessingRoot, "切图");
    [ObservableProperty] private string _resultDirectory = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _png = true;
    [ObservableProperty] private bool _square = true;
    [ObservableProperty] private double _scale = 100;
    [ObservableProperty] private double _positionX = 50;
    [ObservableProperty] private double _positionY = 50;
    public int ImageWidth { get; private set; }
    public int ImageHeight { get; private set; }
    public bool CanEdit => !IsBusy;
    public bool CanExport => CanEdit && Preview is not null;
    public bool HasResult => !string.IsNullOrEmpty(ResultDirectory);
    public Rectangle Crop
    {
        get
        {
            int basisW = Square ? Math.Min(ImageWidth, ImageHeight) : ImageWidth;
            int basisH = Square ? Math.Min(ImageWidth, ImageHeight) : ImageHeight;
            int w = Math.Max(1, (int)Math.Round(basisW * Scale / 100)), h = Math.Max(1, (int)Math.Round(basisH * Scale / 100));
            return new((int)Math.Round((ImageWidth - w) * PositionX / 100), (int)Math.Round((ImageHeight - h) * PositionY / 100), w, h);
        }
    }
    public string Dimensions => Preview is null ? "尚未选择图片" : $"原图 {ImageWidth} × {ImageHeight}  ·  选区 {Crop.Width} × {Crop.Height}\n每块约 {Crop.Width / Mode.Columns} × {Crop.Height / Mode.Rows} 像素（余数分配至相邻块）";
    private CancellationTokenSource? _cancellation;
    private void UpdateCrop() { OnPropertyChanged(nameof(Crop)); OnPropertyChanged(nameof(Dimensions)); }
    partial void OnModeChanged(SplitMode value) => UpdateCrop();
    partial void OnSquareChanged(bool value) => UpdateCrop();
    partial void OnScaleChanged(double value) => UpdateCrop();
    partial void OnPositionXChanged(double value) => UpdateCrop();
    partial void OnPositionYChanged(double value) => UpdateCrop();
    partial void OnResultDirectoryChanged(string value) => OnPropertyChanged(nameof(HasResult));
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanExport)); ExportCommand.NotifyCanExecuteChanged(); }

    public async Task LoadAsync(string path)
    {
        if (IsBusy) return;
        IsBusy = true; Status = "正在载入图片…"; ResultDirectory = "";
        _cancellation = new();
        try
        {
            var token = _cancellation.Token;
            var loaded = await Task.Run(async () =>
            {
                using var image = await ImageSplitService.LoadAsync(path, token);
                int width = image.Width, height = image.Height;
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(1600, 1600), Mode = ResizeMode.Max }));
                using var stream = new MemoryStream();
                await image.SaveAsPngAsync(stream, token); stream.Position = 0;
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                return (width, height, bitmap);
            }, token);
            ImageWidth = loaded.width; ImageHeight = loaded.height; SourcePath = path; Preview = loaded.bitmap;
            Scale = 100; PositionX = PositionY = 50; UpdateCrop(); Status = "拖动选区移动位置；也可用滑块精确调整。";
        }
        catch (OperationCanceledException) { Status = "已取消载入。"; }
        catch (Exception ex) { Status = "载入失败：" + ex.Message; }
        finally { _cancellation.Dispose(); _cancellation = null; IsBusy = false; OnPropertyChanged(nameof(CanExport)); ExportCommand.NotifyCanExecuteChanged(); }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        IsBusy = true; Progress = 0; ResultDirectory = ""; Status = "正在导出…"; _cancellation = new();
        var source = SourcePath; var crop = Crop; var mode = Mode; var output = OutputDirectory; var png = Png; var token = _cancellation.Token;
        var progress = new Progress<double>(value => Progress = value);
        try
        {
            ResultDirectory = await Task.Run(() => new ImageSplitService().ExportAsync(source, crop, mode.Columns, mode.Rows, output, png, progress, token), token);
            Status = $"已导出 {mode.Columns * mode.Rows} 张图片：{ResultDirectory}";
        }
        catch (OperationCanceledException) { Status = "已取消，未保留不完整的导出。"; }
        catch (Exception ex) { Status = "导出失败：" + ex.Message; }
        finally { _cancellation.Dispose(); _cancellation = null; IsBusy = false; }
    }
    [RelayCommand] private void Cancel() => _cancellation?.Cancel();
    [RelayCommand] private void Center() { PositionX = PositionY = 50; Scale = 100; }
}

