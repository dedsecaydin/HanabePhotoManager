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
    private string _outputDirectory = string.Empty;
    private string _targetValue = "2";
    private string _targetUnit = "MB";
    private CompressionTargetMode _targetMode = CompressionTargetMode.PerImage;
    private string _statusText = "拖入图片或选择文件夹开始";
    private string _currentFile = string.Empty;
    private double _progressValue;
    private bool _isRunning;
    private CancellationTokenSource? _cancellation;

    public CompressionViewModel(
        ImageInputDiscovery? discovery = null,
        ImageCompressionPlanner? planner = null,
        ImageCompressionService? service = null)
    {
        _discovery = discovery ?? new ImageInputDiscovery();
        _planner = planner ?? new ImageCompressionPlanner();
        _service = service ?? new ImageCompressionService();
        StartCommand = new AsyncRelayCommand(StartAsync, () => CanStart);
        CancelCommand = new RelayCommand(() => _cancellation?.Cancel(), () => IsRunning);
        ClearCommand = new RelayCommand(Clear, () => !IsRunning && Items.Count > 0);
        RemoveCommand = new RelayCommand<CompressionInputItem>(Remove, item => item is not null && !IsRunning);
    }

    public ObservableCollection<CompressionInputItem> Items { get; } = [];
    public ObservableCollection<CompressionItemResult> Results { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public IReadOnlyList<string> TargetUnits { get; } = ["KB", "MB", "GB"];
    public IReadOnlyList<CompressionTargetChoice> TargetModes { get; } =
    [
        new(CompressionTargetMode.PerImage, "每张图片上限"),
        new(CompressionTargetMode.WholeBatch, "整批总大小")
    ];

    public IAsyncRelayCommand StartCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand<CompressionInputItem> RemoveCommand { get; }

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
    public bool CanStart => !IsRunning && Items.Count > 0 && !string.IsNullOrWhiteSpace(OutputDirectory) && TargetBytes > 0;
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

    public void AddInputs(IEnumerable<string> paths, bool recursive = true)
    {
        var result = _discovery.Discover(paths, recursive, CancellationToken.None);
        var existing = Items.Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in result.Files.Where(existing.Add))
        {
            Items.Add(new CompressionInputItem(path, new FileInfo(path).Length));
        }
        foreach (var warning in result.Warnings) Warnings.Add(warning);
        StatusText = $"已选择 {Items.Count:N0} 张图片";
        OnPropertyChanged(nameof(OriginalTotalBytes));
        NotifyAvailability();
        ClearCommand.NotifyCanExecuteChanged();
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
        if (item is null || IsRunning) return;
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
}

public sealed record CompressionInputItem(string Path, long Length)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string SizeText => FormatBytes(Length);
    private static string FormatBytes(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / 1024d / 1024d:F2} MB"
        : $"{bytes / 1024d:F0} KB";
}

public sealed record CompressionTargetChoice(CompressionTargetMode Value, string Label);
