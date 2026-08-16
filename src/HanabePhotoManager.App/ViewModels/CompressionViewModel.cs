using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Compression;

namespace HanabePhotoManager.App.ViewModels;

public sealed class CompressionViewModel : ObservableObject
{
    private readonly ImageInputDiscovery _discovery;
    private readonly ImageCompressionPlanner _planner;
    private readonly ImageCompressionService _service;
    private readonly ImageCollageService _collageService;
    private ImageToolMode _selectedToolMode = ImageToolMode.Compression;
    private CollageOrientation _collageOrientation = CollageOrientation.Vertical;
    private bool _collageLimitOutputSize;
    private string _outputDirectory = string.Empty;
    private string _targetValue = "2";
    private string _targetUnit = "MB";
    private CompressionTargetMode _targetMode = CompressionTargetMode.PerImage;
    private string _statusText = "拖入图片或选择文件夹开始";
    private string _currentFile = string.Empty;
    private double _progressValue;
    private bool _isRunning;
    private bool _isScanning;
    private CancellationTokenSource? _cancellation;
    private CancellationTokenSource? _inputCancellation;

    public CompressionViewModel(
        ImageInputDiscovery? discovery = null,
        ImageCompressionPlanner? planner = null,
        ImageCompressionService? service = null,
        ImageCollageService? collageService = null)
    {
        _discovery = discovery ?? new ImageInputDiscovery();
        _planner = planner ?? new ImageCompressionPlanner();
        _service = service ?? new ImageCompressionService();
        _collageService = collageService ?? new ImageCollageService();
        StartCommand = new AsyncRelayCommand(StartAsync, () => CanStart);
        CancelCommand = new RelayCommand(CancelCurrentOperation, () => IsRunning || IsScanning);
        ClearCommand = new RelayCommand(Clear, () => !IsRunning && !IsScanning && Items.Count > 0);
        RemoveCommand = new RelayCommand<CompressionInputItem>(Remove, item => item is not null && !IsRunning && !IsScanning);
    }

    public ObservableCollection<CompressionInputItem> Items { get; } = [];
    public ObservableCollection<CompressionItemResult> Results { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public IReadOnlyList<string> TargetUnits { get; } = ["KB", "MB", "GB"];
    public IReadOnlyList<ImageToolModeChoice> ToolModes { get; } =
    [
        new(ImageToolMode.Compression, "批量压缩"),
        new(ImageToolMode.Collage, "拼图"),
        new(ImageToolMode.Watermark, "批量水印"),
        new(ImageToolMode.PixelArt, "像素画"),
    ];
    public IReadOnlyList<CollageOrientationChoice> CollageOrientations { get; } =
    [
        new(CollageOrientation.Vertical, "纵向拼接"),
        new(CollageOrientation.Horizontal, "横向拼接")
    ];
    public IReadOnlyList<CompressionTargetChoice> TargetModes { get; } =
    [
        new(CompressionTargetMode.PerImage, "每张图片上限"),
        new(CompressionTargetMode.WholeBatch, "整批总大小")
    ];

    public IAsyncRelayCommand StartCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand<CompressionInputItem> RemoveCommand { get; }

    public ImageToolMode SelectedToolMode
    {
        get => _selectedToolMode;
        set
        {
            if (!SetProperty(ref _selectedToolMode, value)) return;
            OnPropertyChanged(nameof(IsCompressionMode));
            OnPropertyChanged(nameof(IsCollageMode));
            OnPropertyChanged(nameof(IsWatermarkMode));
            OnPropertyChanged(nameof(IsPixelArtMode));
            OnPropertyChanged(nameof(IsFileOperationMode));
            StatusText = value == ImageToolMode.Collage ? "按队列顺序拼接图片" : "拖入图片或选择文件夹开始";
            NotifyAvailability();
        }
    }
    public bool IsCompressionMode => SelectedToolMode == ImageToolMode.Compression;
    public bool IsCollageMode => SelectedToolMode == ImageToolMode.Collage;
    public bool IsWatermarkMode => SelectedToolMode == ImageToolMode.Watermark;
    public bool IsPixelArtMode => SelectedToolMode == ImageToolMode.PixelArt;
    public bool IsFileOperationMode => !IsWatermarkMode && !IsPixelArtMode;
    public CollageOrientation CollageOrientation
    {
        get => _collageOrientation;
        set => SetProperty(ref _collageOrientation, value);
    }
    public bool CollageLimitOutputSize
    {
        get => _collageLimitOutputSize;
        set
        {
            if (SetProperty(ref _collageLimitOutputSize, value)) NotifyAvailability();
        }
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set { if (SetProperty(ref _outputDirectory, value ?? string.Empty)) NotifyAvailability(); }
    }

    public string TargetValue
    {
        get => _targetValue;
        set { if (SetProperty(ref _targetValue, value ?? string.Empty)) NotifyAvailability(); }
    }

    public string TargetUnit
    {
        get => _targetUnit;
        set { if (SetProperty(ref _targetUnit, value ?? "MB")) { OnPropertyChanged(nameof(TargetBytes)); NotifyAvailability(); } }
    }

    public CompressionTargetMode TargetMode
    {
        get => _targetMode;
        set => SetProperty(ref _targetMode, value);
    }

    public long TargetBytes
    {
        get
        {
            if (!double.TryParse(TargetValue, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) || value <= 0)
                return 0;
            var multiplier = TargetUnit switch { "KB" => 1024d, "GB" => 1024d * 1024 * 1024, _ => 1024d * 1024 };
            var bytes = value * multiplier;
            return bytes >= long.MaxValue ? long.MaxValue : (long)Math.Round(bytes);
        }
    }

    public long OriginalTotalBytes => Items.Sum(item => item.Length);
    public long OutputTotalBytes => Results.Where(result => result.Status == CompressionItemStatus.Success).Sum(result => result.OutputBytes);
    public bool CanStart => !IsRunning
        && !IsScanning
        && Items.Count > 0
        && !string.IsNullOrWhiteSpace(OutputDirectory)
        && (IsCollageMode && !CollageLimitOutputSize || TargetBytes > 0);
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string CurrentFile { get => _currentFile; private set => SetProperty(ref _currentFile, value); }
    public double ProgressValue { get => _progressValue; private set => SetProperty(ref _progressValue, value); }
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetProperty(ref _isRunning, value)) return;
            NotifyAvailability();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (!SetProperty(ref _isScanning, value)) return;
            NotifyAvailability();
            CancelCommand.NotifyCanExecuteChanged();
            ClearCommand.NotifyCanExecuteChanged();
            RemoveCommand.NotifyCanExecuteChanged();
        }
    }

    public async Task AddInputsAsync(
        IEnumerable<string> paths,
        bool recursive = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _inputCancellation?.Cancel();
        _inputCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var scanCancellation = _inputCancellation;
        IsScanning = true;
        ProgressValue = 0;
        CurrentFile = "正在扫描文件夹，可取消…";
        StatusText = "正在扫描图片文件；大型网络文件夹可能需要一些时间。";
        try
        {
        var inputs = paths.ToArray();
        var result = await Task.Run(
            () => DiscoverCompressionInputs(inputs, recursive, scanCancellation.Token),
            scanCancellation.Token).ConfigureAwait(true);
        scanCancellation.Token.ThrowIfCancellationRequested();
        var existing = Items.Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var input in result.Inputs.Where(input => existing.Add(input.Path)))
        {
            Items.Add(input);
        }
        foreach (var warning in result.Warnings) Warnings.Add(warning);
        StatusText = $"已选择 {Items.Count:N0} 张图片";
        OnPropertyChanged(nameof(OriginalTotalBytes));
        NotifyAvailability();
        ClearCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException) when (scanCancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(_inputCancellation, scanCancellation))
                StatusText = "已取消扫描。";
            throw;
        }
        finally
        {
            if (ReferenceEquals(_inputCancellation, scanCancellation))
            {
                CurrentFile = string.Empty;
                IsScanning = false;
                _inputCancellation = null;
            }
            scanCancellation.Dispose();
        }
    }

    private async Task StartAsync()
    {
        if (!CanStart) return;
        Results.Clear();
        _cancellation = new CancellationTokenSource();
        IsRunning = true;
        ProgressValue = 0;
        try
        {
            if (IsCollageMode)
            {
                var progress = new Progress<CollageProgress>(report =>
                {
                    CurrentFile = report.CurrentFile;
                    ProgressValue = report.Total == 0 ? 0 : report.Processed * 80d / report.Total;
                });
                var result = await _collageService.ComposeAsync(
                    Items.Select(item => item.Path).ToArray(),
                    new CollageOptions(OutputDirectory, CollageOrientation,
                        CollageLimitOutputSize ? TargetBytes : null),
                    progress,
                    _cancellation.Token).ConfigureAwait(true);
                Results.Add(new CompressionItemResult(
                    "拼图",
                    result.OutputPath,
                    result.IsSuccess ? CompressionItemStatus.Success : CompressionItemStatus.Unreachable,
                    OriginalTotalBytes,
                    result.OutputBytes,
                    result.Quality,
                    result.Message));
                ProgressValue = 100;
                OnPropertyChanged(nameof(OutputTotalBytes));
                StatusText = result.Message;
            }
            else
            {
                var sources = Items.Select(item => new CompressionSource(item.Path, item.Length, 1)).ToArray();
                var plan = _planner.CreatePlan(sources, TargetMode, TargetBytes);
                for (var index = 0; index < plan.Count; index++)
                {
                    _cancellation.Token.ThrowIfCancellationRequested();
                    CurrentFile = Path.GetFileName(plan[index].Source.Path);
                    var result = await _service.CompressAsync(plan[index], new CompressionOptions(OutputDirectory), _cancellation.Token)
                        .ConfigureAwait(true);
                    Results.Add(result);
                    ProgressValue = (index + 1d) * 100 / plan.Count;
                    OnPropertyChanged(nameof(OutputTotalBytes));
                }
                StatusText = $"完成：{Results.Count(result => result.Status == CompressionItemStatus.Success)} 成功，{Results.Count(result => result.Status != CompressionItemStatus.Success)} 未输出";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消；已完成的输出已保留";
        }
        finally
        {
            CurrentFile = string.Empty;
            IsRunning = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    private void Remove(CompressionInputItem? item)
    {
        if (item is null || IsRunning || IsScanning) return;
        Items.Remove(item);
        OnPropertyChanged(nameof(OriginalTotalBytes));
        NotifyAvailability();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private void Clear()
    {
        Items.Clear();
        Results.Clear();
        Warnings.Clear();
        StatusText = "拖入图片或选择文件夹开始";
        OnPropertyChanged(nameof(OriginalTotalBytes));
        OnPropertyChanged(nameof(OutputTotalBytes));
        NotifyAvailability();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(TargetBytes));
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
    }

    private void CancelCurrentOperation()
    {
        _inputCancellation?.Cancel();
        _cancellation?.Cancel();
    }

    private CompressionInputScan DiscoverCompressionInputs(
        IEnumerable<string> paths,
        bool recursive,
        CancellationToken cancellationToken)
    {
        var discovery = _discovery.Discover(paths, recursive, cancellationToken);
        var inputs = new List<CompressionInputItem>(discovery.Files.Count);
        var warnings = discovery.Warnings.ToList();
        foreach (var path in discovery.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                inputs.Add(new CompressionInputItem(path, new FileInfo(path).Length));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"无法读取图片：{path}（{ex.Message}）");
            }
        }

        return new CompressionInputScan(inputs, warnings);
    }
}

internal sealed record CompressionInputScan(
    IReadOnlyList<CompressionInputItem> Inputs,
    IReadOnlyList<string> Warnings);

public sealed record CompressionInputItem(string Path, long Length)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string SizeText => FormatBytes(Length);
    private static string FormatBytes(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / 1024d / 1024d:F2} MB"
        : $"{bytes / 1024d:F0} KB";
}

public sealed record CompressionTargetChoice(CompressionTargetMode Value, string Label);
public enum ImageToolMode
{
    Compression,
    Collage,
    Watermark,
    PixelArt,
}

public sealed record ImageToolModeChoice(ImageToolMode Value, string Label);
public sealed record CollageOrientationChoice(CollageOrientation Value, string Label);
