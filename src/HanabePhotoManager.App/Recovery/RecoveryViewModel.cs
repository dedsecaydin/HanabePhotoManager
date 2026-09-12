using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.Core.Recovery;
using Microsoft.Win32;

namespace HanabePhotoManager.App.Recovery;

public sealed class RecoveryViewModel : ObservableObject
{
    private RecoveryImageService _service = new();
    private RecoveryDeviceSession? _deviceSession;
    private bool _useDevice;
    public ObservableCollection<string> Devices { get; } = [];
    private string? _selectedDevice;
    public string? SelectedDevice { get => _selectedDevice; set { SetProperty(ref _selectedDevice, value); ChooseDeviceCommand.NotifyCanExecuteChanged(); } }
    public IRelayCommand RefreshDevicesCommand { get; }
    public IRelayCommand ChooseDeviceCommand { get; }
    private CancellationTokenSource? _cancellation;
    private CancellationTokenSource? _previewCancellation;
    private readonly SemaphoreSlim _previewGate = new(1, 1);
    private System.Windows.Media.Imaging.BitmapSource? _previewImage;
    private string _previewStatus = "选中照片后自动检查预览。";
    private string _lastOutputFile = string.Empty;
    public System.Windows.Media.Imaging.BitmapSource? PreviewImage { get => _previewImage; private set => SetProperty(ref _previewImage, value); }
    public string PreviewStatus { get => _previewStatus; private set => SetProperty(ref _previewStatus, value); }
    private string _imagePath = string.Empty;
    private string _outputDirectory = string.Empty;
    public string OutputDirectory { get => _outputDirectory; private set => SetProperty(ref _outputDirectory, value); }
    private string _statusText = "请选择 相机存储卡的 .img 或 .raw 镜像。";
    private double _progressValue;
    private bool _isBusy;
    private RecoveryCandidate? _selectedCandidate;
    private RecoveryScanResult? _scan;
    private bool _useWriteTimeRange;
    private bool _includeUnknownTime = true;
    private DateTime? _writtenFromDate = DateTime.Today, _writtenToDate = DateTime.Today;
    private string _writtenFromClock = "00:00:00", _writtenToClock = "23:59:59";
    public bool UseWriteTimeRange { get => _useWriteTimeRange; set => SetProperty(ref _useWriteTimeRange, value); }
    public bool IncludeUnknownTime { get => _includeUnknownTime; set => SetProperty(ref _includeUnknownTime, value); }
    public DateTime? WrittenFromDate { get => _writtenFromDate; set => SetProperty(ref _writtenFromDate, value); }
    public DateTime? WrittenToDate { get => _writtenToDate; set => SetProperty(ref _writtenToDate, value); }
    public string WrittenFromClock { get => _writtenFromClock; set => SetProperty(ref _writtenFromClock, value); }
    public string WrittenToClock { get => _writtenToClock; set => SetProperty(ref _writtenToClock, value); }
    public bool CanEditScanOptions => !IsBusy;

    public RecoveryViewModel()
    {
        ChooseImageCommand = new RelayCommand(ChooseImage, () => !IsBusy);
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy && (_useDevice || File.Exists(ImagePath)));
        RefreshDevicesCommand = new RelayCommand(RefreshDevices, () => !IsBusy);
        ChooseDeviceCommand = new RelayCommand(() =>
        {
            _deviceSession?.Dispose(); _deviceSession = null;
            _useDevice = true;
            ImagePath = SelectedDevice!;
            OutputDirectory = RecoveryOutputDirectory.CreatePath("存储卡_" + ImagePath[0], DateTime.Now);
            ResetScan();
            StatusText = "已选择存储卡。开始扫描时将请求 Windows 只读访问权限；输出目录已自动安排。";
        }, () => !IsBusy && SelectedDevice is not null);
        CancelCommand = new RelayCommand(() => _cancellation?.Cancel(), () => IsBusy);
        RecoverCommand = new AsyncRelayCommand(RecoverAsync, () => !IsBusy && _scan is not null && SelectedCandidate?.CanRecoverDirectly == true && _scan.Candidates.Contains(SelectedCandidate));
        OpenOutputCommand = new RelayCommand(OpenOutput, () => Directory.Exists(LastOutputDirectory));
        OpenRecoveredFileCommand = new RelayCommand(() =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_lastOutputFile) { UseShellExecute = true }); }
            catch (Exception ex) { StatusText = "无法打开恢复文件：" + ex.Message; }
        }, () => File.Exists(_lastOutputFile));
        RefreshDevices();
    }

    private void RefreshDevices()
    {
        try
        {
            var selected = SelectedDevice;
            Devices.Clear();
            foreach (var drive in DriveInfo.GetDrives())
                if (drive.DriveType == DriveType.Removable) Devices.Add(drive.Name);
            SelectedDevice = Devices.Contains(selected ?? "") ? selected : Devices.FirstOrDefault();
        }
        catch (Exception ex) { StatusText = "设备列表读取失败：" + ex.Message; }
    }

    public ObservableCollection<RecoveryCandidate> Candidates { get; } = [];
    public IRelayCommand ChooseImageCommand { get; }
    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand RecoverCommand { get; }
    public IRelayCommand OpenOutputCommand { get; }
    public IRelayCommand OpenRecoveredFileCommand { get; }
    public string LastOutputDirectory { get; private set; } = string.Empty;
    public string CandidateSummary => $"共 {Candidates.Count:N0} 个 · 可导出 {Candidates.Count(c => c.CanRecoverDirectly):N0} 个 · 需进一步分析 {Candidates.Count(c => !c.CanRecoverDirectly):N0} 个";
    public bool ShowEmptyState => !IsBusy && !HasCandidates;
    public string EmptyStateText => string.IsNullOrEmpty(ImagePath) ? "1. 准备存储卡镜像\n2. 打开 .img 或 .raw 文件\n3. 扫描并查看候选证据\n4. 导出到独立文件夹" : "暂无候选。点击开始扫描；扫描完成后仍为空表示未找到支持的完整 媒体结构。";
    public string CandidateEvidence => SelectedCandidate is not { } c ? "选中候选后查看结构证据与导出条件。" :
        c.IsRawPhoto ? $"{c.TimeDescription}\n大小：{c.SizeDescription}\n{c.StructureEvidence}" :
        c.IsJpeg ? $"{c.TimeDescription}\n大小：{c.SizeDescription}\nJPEG 帧、扫描数据与结束标记：已识别\n连续数据：{(!c.IsFragmented ? "是" : "未确认")}\n{(c.CanRecoverDirectly ? "可导出照片副本，请打开检查画面完整性。" : "目录长度或连续性未确认，暂不支持直接导出。")} " :
        $"{c.TimeDescription}\n范围：{c.StartOffset:N0}–{c.EndOffset:N0} 字节\n大小：{c.Length / 1048576d:N2} MB\n文件类型 ftyp：{Evidence(c.HasFtyp)}\n媒体数据 mdat：{Evidence(c.HasMdat)}\n媒体索引 moov：{Evidence(c.HasMoov)}\n采样表：{Evidence(c.HasSampleTables)}\n\n" +
        (c.CanRecoverDirectly ? "结构检查通过，可导出连续数据副本。仍需用播放器确认画面、声音及完整时长。" : "结构证据不足，暂不支持自动导出。此工具不会重建缺失索引或拼接碎片。" );
    private static string Evidence(bool present) => present ? "已识别" : "未识别";
    private void OpenOutput()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LastOutputDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { StatusText = "无法打开输出目录：" + ex.Message; }
    }

    public string ImagePath { get => _imagePath; private set { if (SetProperty(ref _imagePath, value)) NotifyCommands(); } }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public double ProgressValue { get => _progressValue; private set => SetProperty(ref _progressValue, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { NotifyCommands(); OnPropertyChanged(nameof(ShowEmptyState)); OnPropertyChanged(nameof(CanEditScanOptions)); } } }
    public bool HasCandidates => Candidates.Count > 0;
    public bool IsExFat => _scan?.IsExFat == true;
    public string FileSystemSummary => _scan is null ? "尚未扫描" : _scan.IsExFat ? $"exFAT · 扇区 {_scan.SectorSize:N0} B · 簇 {_scan.ClusterSize:N0} B" : "未识别为 exFAT";
    public RecoveryCandidate? SelectedCandidate { get => _selectedCandidate; set { if (SetProperty(ref _selectedCandidate, value)) { NotifyCommands(); OnPropertyChanged(nameof(CandidateEvidence)); _ = LoadPreviewAsync(value); } } }

    private async Task LoadPreviewAsync(RecoveryCandidate? candidate)
    {
        _previewCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        PreviewImage = null;
        PreviewStatus = candidate is null ? "选中照片后自动检查预览。" : "正在检查像素预览…";
        try
        {
            if (candidate is null) return;
            if (_useDevice) { PreviewStatus = "设备扫描候选请导出后打开检查；当前保留结构证据。"; return; }
            await _previewGate.WaitAsync(cancellation.Token);
            try
            {
                var result = await RecoveryPreviewService.LoadAsync(ImagePath, candidate, cancellation.Token);
                if (cancellation.IsCancellationRequested || !ReferenceEquals(_previewCancellation, cancellation)) return;
                PreviewImage = result.Image;
                PreviewStatus = result.Description;
            }
            finally { _previewGate.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (ReferenceEquals(_previewCancellation, cancellation)) PreviewStatus = "预览检查失败：" + ex.Message; }
        finally { if (ReferenceEquals(_previewCancellation, cancellation)) _previewCancellation = null; cancellation.Dispose(); }
    }

    private void ChooseImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "打开相机存储卡镜像", Filter = "Raw image|*.img;*.raw", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        _deviceSession?.Dispose(); _deviceSession = null;
        _useDevice = false; _service = new RecoveryImageService();
        ImagePath = dialog.FileName;
        OutputDirectory = RecoveryOutputDirectory.CreatePath(ImagePath, DateTime.Now);
        ResetScan();
        StatusText = "镜像只读打开；可以开始扫描。";
    }

    private async Task ScanAsync()
    {
        DateTime? from = null, to = null;
        if (UseWriteTimeRange)
        {
            if (WrittenFromDate is null || WrittenToDate is null ||
                !TimeSpan.TryParseExact(WrittenFromClock, @"hh\:mm\:ss", null, out var startClock) ||
                !TimeSpan.TryParseExact(WrittenToClock, @"hh\:mm\:ss", null, out var endClock) || startClock.TotalHours >= 24 || endClock.TotalHours >= 24)
            { StatusText = "请选择起止日期，时间格式为 HH:mm:ss（例如 14:30:00）。"; return; }
            from = WrittenFromDate.Value.Date + startClock;
            to = WrittenToDate.Value.Date + endClock;
            if (from > to) { StatusText = "结束时间不能早于开始时间。"; return; }
            to = to.Value.AddSeconds(1).AddTicks(-1);
        }
        ResetScan();
        _cancellation = new CancellationTokenSource(); IsBusy = true;
        try
        {
            if (string.IsNullOrEmpty(OutputDirectory)) OutputDirectory = RecoveryOutputDirectory.CreatePath(ImagePath, DateTime.Now);
            RecoveryOutputDirectory.Validate(OutputDirectory);
            if (_useDevice)
            {
                StatusText = "正在启动独立只读进程，并核对来源盘与输出盘…";
                _deviceSession?.Dispose(); _deviceSession = null;
                _deviceSession = await RecoveryDeviceSession.ConnectAsync(ImagePath, _cancellation.Token);
                var session = _deviceSession;
                _service = new RecoveryImageService(ImagePath, session.OpenRead, output =>
                {
                    if (!string.Equals(Path.GetPathRoot(Path.GetFullPath(output)), @"D:\", StringComparison.OrdinalIgnoreCase))
                        throw new IOException("设备恢复只能写入已核对的 D 盘输出位置。");
                    RecoveryOutputDirectory.Validate(output);
                    session.ValidateDestination();
                });
            }
            RecoveryOutputDirectory.Prepare(OutputDirectory);
            using var deviceCancellation = _cancellation.Token.Register(() => { if (_useDevice) _deviceSession?.Dispose(); });
            StatusText = "正在只读扫描 exFAT 与 媒体结构…";
            var discovered = new Progress<RecoveryCandidate>(candidate =>
            {
                Candidates.Add(candidate);
                SelectedCandidate ??= candidate;
                OnPropertyChanged(nameof(HasCandidates));
                OnPropertyChanged(nameof(CandidateSummary));
                StatusText = $"正在分析候选，已发现 {Candidates.Count:N0} 个…";
            });
            var progress = new Progress<double>(value => ProgressValue = value);
            var includeUnknown = IncludeUnknownTime;
            _scan = await Task.Run(() => _service.ScanAsync(ImagePath, progress, _cancellation.Token, discovered, from, to, includeUnknown));
            SelectedCandidate = Candidates.FirstOrDefault();
            OnPropertyChanged(nameof(HasCandidates)); OnPropertyChanged(nameof(IsExFat)); OnPropertyChanged(nameof(FileSystemSummary));
            StatusText = Candidates.Count == 0 ? "扫描完成，没有找到可识别的 媒体候选。" : $"扫描完成：{Candidates.Count:N0} 个候选；仅完整结构可直接恢复。";
        }
        catch (OperationCanceledException) { CloseFailedDevice(); StatusText = "扫描已取消；镜像和原卡均未修改。"; }
        catch (Exception ex) { CloseFailedDevice(); StatusText = _cancellation.IsCancellationRequested ? "扫描已停止；来源未修改。再次扫描将重新连接设备。" : "扫描失败：" + ex.Message; }
        finally { IsBusy = false; _cancellation.Dispose(); _cancellation = null; }
    }

    private async Task RecoverAsync()
    {
        if (_scan is null || SelectedCandidate is null) return;
        var scan = _scan;
        var candidate = SelectedCandidate;
        _cancellation = new CancellationTokenSource(); IsBusy = true; ProgressValue = 0;
        try
        {
            if (string.IsNullOrEmpty(OutputDirectory)) OutputDirectory = RecoveryOutputDirectory.CreatePath(ImagePath, DateTime.Now);
            RecoveryOutputDirectory.Validate(OutputDirectory, checked(candidate.Length * 2 + 1024 * 1024));
            using var deviceCancellation = _cancellation.Token.Register(() => { if (_useDevice) _deviceSession?.Dispose(); });
            StatusText = "正在保留原始候选并生成恢复副本…";
            var progress = new Progress<double>(value => ProgressValue = value);
            var path = await Task.Run(() => _service.ExportDirectAsync(scan, candidate, OutputDirectory, progress, _cancellation.Token));
            StatusText = "恢复完成：" + path;
            LastOutputDirectory = Path.GetDirectoryName(path)!;
            _lastOutputFile = path;
            OpenRecoveredFileCommand.NotifyCanExecuteChanged();
            OpenOutputCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException) { CloseFailedDevice(); StatusText = "恢复已取消；来源未修改。"; }
        catch (Exception ex) { CloseFailedDevice(); StatusText = _cancellation.IsCancellationRequested ? "恢复已停止；来源未修改。设备模式需重新扫描。" : "恢复失败：" + ex.Message; }
        finally { IsBusy = false; _cancellation.Dispose(); _cancellation = null; }
    }

    private void CloseFailedDevice()
    {
        if (!_useDevice) return;
        _deviceSession?.Dispose(); _deviceSession = null;
        _scan = null;
    }

    private void NotifyCommands()
    {
        ChooseImageCommand.NotifyCanExecuteChanged(); ScanCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); RecoverCommand.NotifyCanExecuteChanged();
        ChooseDeviceCommand.NotifyCanExecuteChanged(); RefreshDevicesCommand.NotifyCanExecuteChanged();
    }

    private void ResetScan()
    {
        _scan = null;
        SelectedCandidate = null;
        Candidates.Clear();
        ProgressValue = 0;
        OnPropertyChanged(nameof(HasCandidates));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(CandidateSummary));
        OnPropertyChanged(nameof(FileSystemSummary));
        OnPropertyChanged(nameof(IsExFat));
        NotifyCommands();
    }
}
