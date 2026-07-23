using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace HanabePhotoManager.App.Watermark;

public sealed partial class WatermarkQueueItem : ObservableObject
{
    public WatermarkQueueItem(string path) { Path = path; Name = System.IO.Path.GetFileName(path); }
    public string Path { get; }
    public string Name { get; }
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private string _status = "等待";
    [ObservableProperty] private bool _useIndividualSettings;
    [ObservableProperty] private double _centerX = .86;
    [ObservableProperty] private double _centerY = .88;
    [ObservableProperty] private double _sizeRatio = .18;
    [ObservableProperty] private double _opacity = .72;
}

public sealed partial class WatermarkViewModel : ObservableObject
{
    private readonly WatermarkInputDiscovery _discovery = new();
    private readonly WatermarkExportService _exporter = new();
    private WatermarkQueueItem? _observedSelectedItem;
    private CancellationTokenSource? _cts;
    public ObservableCollection<WatermarkQueueItem> Items { get; } = [];
    [ObservableProperty] private WatermarkQueueItem? _selectedItem;
    [ObservableProperty] private string _watermarkPath = "";
    [ObservableProperty] private string _outputDirectory = "";
    [ObservableProperty] private string _suffix = "_watermarked";
    [ObservableProperty] private bool _recursive = true;
    [ObservableProperty] private bool _preserveMetadata = true;
    [ObservableProperty] private bool _isTiled;
    [ObservableProperty] private bool _isManualTile;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _centerX = .86;
    [ObservableProperty] private double _centerY = .88;
    [ObservableProperty] private double _sizeRatio = .18;
    [ObservableProperty] private double _opacity = .72;
    [ObservableProperty] private double _density = .5;
    [ObservableProperty] private double _horizontalGap = .2;
    [ObservableProperty] private double _verticalGap = .2;
    [ObservableProperty] private double _angle = -24;
    [ObservableProperty] private bool _stagger = true;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusText = "添加图片和透明 PNG 水印后即可导出。";
    [ObservableProperty] private BitmapImage? _previewImage;

    public bool HasItems => Items.Count > 0;
    public bool ShowSignatureSettings => !IsTiled;
    public bool ShowTileSettings => IsTiled;
    public bool ShowManualTileSettings => IsTiled && IsManualTile;
    public bool CanExport => HasItems && File.Exists(WatermarkPath) && Directory.Exists(OutputDirectory) && !IsBusy;

    partial void OnIsTiledChanged(bool value) { OnPropertyChanged(nameof(ShowSignatureSettings)); OnPropertyChanged(nameof(ShowTileSettings)); OnPropertyChanged(nameof(ShowManualTileSettings)); }
    partial void OnIsManualTileChanged(bool value) => OnPropertyChanged(nameof(ShowManualTileSettings));
    partial void OnWatermarkPathChanged(string value) => OnPropertyChanged(nameof(CanExport));
    partial void OnOutputDirectoryChanged(string value) => OnPropertyChanged(nameof(CanExport));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanExport));
    partial void OnSelectedItemChanged(WatermarkQueueItem? value)
    {
        if (_observedSelectedItem is not null) _observedSelectedItem.PropertyChanged -= SelectedItem_PropertyChanged;
        _observedSelectedItem = value;
        if (_observedSelectedItem is not null) _observedSelectedItem.PropertyChanged += SelectedItem_PropertyChanged;
        if (value is not null) LoadPreview(value.Path);
        OnPropertyChanged(nameof(HasSelectedItem));
        NotifyPreviewSettings();
    }
    public bool HasSelectedItem => SelectedItem is not null;
    public double PreviewOpacity => SelectedItem?.UseIndividualSettings == true ? SelectedItem.Opacity : Opacity;
    public double PreviewWatermarkWidth => 60 + ((SelectedItem?.UseIndividualSettings == true ? SelectedItem.SizeRatio : SizeRatio) * 500);

    partial void OnOpacityChanged(double value) => NotifyPreviewSettings();
    partial void OnSizeRatioChanged(double value) => NotifyPreviewSettings();

    private void SelectedItem_PropertyChanged(object? sender, PropertyChangedEventArgs e) => NotifyPreviewSettings();
    private void NotifyPreviewSettings()
    {
        OnPropertyChanged(nameof(PreviewOpacity));
        OnPropertyChanged(nameof(PreviewWatermarkWidth));
    }

    [RelayCommand] private void ChooseImages()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "图片|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff" };
        if (dialog.ShowDialog() == true) AddInputs(dialog.FileNames);
    }
    [RelayCommand] private void ChooseFolder()
    { using var dialog = new WinForms.FolderBrowserDialog(); if (dialog.ShowDialog() == WinForms.DialogResult.OK) AddInputs([dialog.SelectedPath]); }
    [RelayCommand] private void ChooseWatermark()
    { var d = new Microsoft.Win32.OpenFileDialog { Filter = "透明 PNG|*.png" }; if (d.ShowDialog() == true) WatermarkPath = d.FileName; }
    [RelayCommand] private void ChooseOutput()
    { using var d = new WinForms.FolderBrowserDialog(); if (d.ShowDialog() == WinForms.DialogResult.OK) OutputDirectory = d.SelectedPath; }
    [RelayCommand] private void SelectAll() { foreach (var item in Items) item.IsSelected = true; }
    [RelayCommand] private void RemoveSelected() { foreach (var item in Items.Where(x => x.IsSelected).ToArray()) Items.Remove(item); SelectedItem = Items.FirstOrDefault(); ChangedItems(); }
    [RelayCommand] private void Clear() { Items.Clear(); SelectedItem = null; PreviewImage = null; ChangedItems(); }
    [RelayCommand] private void Cancel() => _cts?.Cancel();
    [RelayCommand] private void Place(string? cell)
    {
        var values = cell?.Split(','); if (values?.Length != 2) return;
        SetNormalizedPosition(double.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    public void AddInputs(IEnumerable<string> paths)
    {
        var result = _discovery.Discover(paths, Recursive);
        var known = Items.Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in result.Files) if (known.Add(file)) Items.Add(new(file) { CenterX = CenterX, CenterY = CenterY, SizeRatio = SizeRatio, Opacity = Opacity });
        if (SelectedItem is null && Items.Count > 0) SelectedItem = Items[0];
        StatusText = result.Warnings.Count == 0 ? $"已添加 {Items.Count:N0} 张图片。" : $"已添加 {Items.Count:N0} 张，{result.Warnings.Count} 个路径被跳过。";
        ChangedItems();
    }

    public void SetWatermark(string path) { if (string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)) WatermarkPath = path; else StatusText = "水印必须是 PNG 文件。"; }
    public void SetNormalizedPosition(double x, double y)
    {
        var normalizedX = Math.Clamp(x, 0, 1); var normalizedY = Math.Clamp(y, 0, 1);
        if (SelectedItem?.UseIndividualSettings == true) { SelectedItem.CenterX = normalizedX; SelectedItem.CenterY = normalizedY; }
        else { CenterX = normalizedX; CenterY = normalizedY; }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (!CanExport) { StatusText = "请先添加图片、水印并选择导出目录。"; return; }
        IsBusy = true; Progress = 0; _cts = new(); var selected = Items.Where(x => x.IsSelected).ToArray();
        try
        {
            var results = new List<WatermarkExportResult>();
            for (var index = 0; index < selected.Length; index++)
            {
                var item = selected[index];
                var itemCenterX = item.UseIndividualSettings ? item.CenterX : CenterX;
                var itemCenterY = item.UseIndividualSettings ? item.CenterY : CenterY;
                var itemSize = item.UseIndividualSettings ? item.SizeRatio : SizeRatio;
                var itemOpacity = item.UseIndividualSettings ? item.Opacity : Opacity;
                var options = new WatermarkExportOptions(OutputDirectory, Suffix, PreserveMetadata, IsTiled ? WatermarkMode.Tiled : WatermarkMode.Signature,
                    new(itemCenterX, itemCenterY, itemSize, itemOpacity), new(!IsManualTile, Density, HorizontalGap, VerticalGap, Angle, Stagger, itemOpacity, itemSize));
                var itemResults = await _exporter.ExportAsync([item.Path], WatermarkPath, options, null, _cts.Token);
                results.AddRange(itemResults);
                Progress = (index + 1) * 100d / selected.Length;
                StatusText = $"正在导出 {index + 1}/{selected.Length} · {item.Name}";
            }
            foreach (var item in selected) item.Status = results.First(x => x.SourcePath == item.Path).Status == WatermarkExportStatus.Success ? "完成" : "失败";
            StatusText = $"导出完成：{results.Count(x => x.Status == WatermarkExportStatus.Success)} 成功，{results.Count(x => x.Status != WatermarkExportStatus.Success)} 失败。";
        }
        catch (OperationCanceledException) { StatusText = "已取消；未完成的临时文件已清理。"; }
        catch (Exception ex) { StatusText = ex.Message; }
        finally { IsBusy = false; _cts.Dispose(); _cts = null; }
    }

    private void ChangedItems() { OnPropertyChanged(nameof(HasItems)); OnPropertyChanged(nameof(CanExport)); }
    private void LoadPreview(string path)
    { try { var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 1200; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); PreviewImage = image; } catch { } }
}
