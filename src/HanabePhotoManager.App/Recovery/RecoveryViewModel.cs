using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.Core.Recovery;
using Microsoft.Win32;

namespace HanabePhotoManager.App.Recovery;

public sealed class RecoveryViewModel : ObservableObject
{
    private readonly RecoveryImageService _service = new();
    private CancellationTokenSource? _cancellation;
    private string _imagePath = string.Empty;
    private string _statusText = "请选择 Sony 相机卡的 .img 或 .raw 镜像。";
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
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy && File.Exists(ImagePath));
        CancelCommand = new RelayCommand(() => _cancellation?.Cancel(), () => IsBusy);
        RecoverCommand = new AsyncRelayCommand(RecoverAsync, () => !IsBusy && _scan is not null && SelectedCandidate?.CanRecoverDirectly == true && _scan.Candidates.Contains(SelectedCandidate));
        OpenOutputCommand = new RelayCommand(OpenOutput, () => Directory.Exists(LastOutputDirectory));
    }

    public ObservableCollection<RecoveryCandidate> Candidates { get; } = [];
    public IRelayCommand ChooseImageCommand { get; }
    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand RecoverCommand { get; }
    public IRelayCommand OpenOutputCommand { get; }
    public string LastOutputDirectory { get; private set; } = string.Empty;
    public string CandidateSummary => $"共 {Candidates.Count:N0} 个 · 可导出 {Candidates.Count(c => c.CanRecoverDirectly):N0} 个 · 需进一步分析 {Candidates.Count(c => !c.CanRecoverDirectly):N0} 个";
    public bool ShowEmptyState => !IsBusy && !HasCandidates;
    public string EmptyStateText => string.IsNullOrEmpty(ImagePath) ? "1. 准备存储卡镜像\n2. 打开 .img 或 .raw 文件\n3. 扫描并查看候选证据\n4. 导出到独立文件夹" : "暂无候选。点击开始扫描；扫描完成后仍为空表示未找到支持的完整 媒体结构。";
    public string CandidateEvidence => SelectedCandidate is not { } c ? "选中候选后查看结构证据与导出条件。" :
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
    public RecoveryCandidate? SelectedCandidate { get => _selectedCandidate; set { if (SetProperty(ref _selectedCandidate, value)) { NotifyCommands(); OnPropertyChanged(nameof(CandidateEvidence)); } } }

    private void ChooseImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "打开相机存储卡镜像", Filter = "Raw image|*.img;*.raw", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        ImagePath = dialog.FileName;
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
        catch (OperationCanceledException) { StatusText = "扫描已取消；镜像和原卡均未修改。"; }
        catch (Exception ex) { StatusText = "扫描失败：" + ex.Message; }
        finally { IsBusy = false; _cancellation.Dispose(); _cancellation = null; }
    }

    private async Task RecoverAsync()
    {
        if (_scan is null || SelectedCandidate is null) return;
        var dialog = new OpenFolderDialog { Title = "选择恢复输出目录" };
        if (dialog.ShowDialog() != true) return;
        _cancellation = new CancellationTokenSource(); IsBusy = true; ProgressValue = 0;
        try
        {
            StatusText = "正在保留原始候选并生成恢复副本…";
            var path = await _service.ExportDirectAsync(_scan, SelectedCandidate, dialog.FolderName, new Progress<double>(value => ProgressValue = value), _cancellation.Token);
            StatusText = "恢复完成：" + path;
            LastOutputDirectory = Path.GetDirectoryName(path)!;
            OpenOutputCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException) { StatusText = "恢复已取消；原镜像未修改。"; }
        catch (Exception ex) { StatusText = "恢复失败：" + ex.Message; }
        finally { IsBusy = false; _cancellation.Dispose(); _cancellation = null; }
    }

    private void NotifyCommands()
    {
        ChooseImageCommand.NotifyCanExecuteChanged(); ScanCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); RecoverCommand.NotifyCanExecuteChanged();
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
