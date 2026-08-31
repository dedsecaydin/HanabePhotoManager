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

    public RecoveryViewModel()
    {
        ChooseImageCommand = new RelayCommand(ChooseImage, () => !IsBusy);
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy && File.Exists(ImagePath));
        CancelCommand = new RelayCommand(() => _cancellation?.Cancel(), () => IsBusy);
        RecoverCommand = new AsyncRelayCommand(RecoverAsync, () => !IsBusy && SelectedCandidate?.CanRecoverDirectly == true);
    }

    public ObservableCollection<RecoveryCandidate> Candidates { get; } = [];
    public IRelayCommand ChooseImageCommand { get; }
    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand RecoverCommand { get; }

    public string ImagePath { get => _imagePath; private set { if (SetProperty(ref _imagePath, value)) NotifyCommands(); } }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public double ProgressValue { get => _progressValue; private set => SetProperty(ref _progressValue, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCommands(); } }
    public bool HasCandidates => Candidates.Count > 0;
    public bool IsExFat => _scan?.IsExFat == true;
    public string FileSystemSummary => _scan is null ? "尚未扫描" : _scan.IsExFat ? $"exFAT · 扇区 {_scan.SectorSize:N0} B · 簇 {_scan.ClusterSize:N0} B" : "未识别为 exFAT";
    public RecoveryCandidate? SelectedCandidate { get => _selectedCandidate; set { if (SetProperty(ref _selectedCandidate, value)) NotifyCommands(); } }

    private void ChooseImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "打开相机存储卡镜像", Filter = "Raw image|*.img;*.raw", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        ImagePath = dialog.FileName;
        StatusText = "镜像只读打开；可以开始扫描。";
    }

    private async Task ScanAsync()
    {
        _cancellation = new CancellationTokenSource(); IsBusy = true; ProgressValue = 0; Candidates.Clear();
        try
        {
            StatusText = "正在只读扫描 exFAT 与 MP4 结构…";
            var discovered = new Progress<RecoveryCandidate>(candidate =>
            {
                Candidates.Add(candidate);
                SelectedCandidate ??= candidate;
                OnPropertyChanged(nameof(HasCandidates));
                StatusText = $"正在分析候选，已发现 {Candidates.Count:N0} 个…";
            });
            _scan = await _service.ScanAsync(ImagePath, new Progress<double>(value => ProgressValue = value), _cancellation.Token, discovered);
            SelectedCandidate = Candidates.FirstOrDefault();
            OnPropertyChanged(nameof(HasCandidates)); OnPropertyChanged(nameof(IsExFat)); OnPropertyChanged(nameof(FileSystemSummary));
            StatusText = Candidates.Count == 0 ? "扫描完成，没有找到可识别的 MP4 候选。" : $"扫描完成：{Candidates.Count:N0} 个候选；仅完整结构可直接恢复。";
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
        }
        catch (OperationCanceledException) { StatusText = "恢复已取消；原镜像未修改。"; }
        catch (Exception ex) { StatusText = "恢复失败：" + ex.Message; }
        finally { IsBusy = false; _cancellation.Dispose(); _cancellation = null; }
    }

    private void NotifyCommands()
    {
        ChooseImageCommand.NotifyCanExecuteChanged(); ScanCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); RecoverCommand.NotifyCanExecuteChanged();
    }
}
