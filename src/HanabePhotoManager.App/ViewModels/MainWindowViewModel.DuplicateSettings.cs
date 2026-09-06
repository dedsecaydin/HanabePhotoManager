using CommunityToolkit.Mvvm.Input;

namespace HanabePhotoManager.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool _useDuplicateHashCache = true;
    private int _duplicateHashParallelism = 2;
    private int _visualDuplicateThreshold = 8;
    private bool _showSimilarityDifferenceGrid = true;
    private bool _promptImportResumeAtStartup = true;
    private IRelayCommand? _clearDuplicateHashCacheCommand;

    public bool UseDuplicateHashCache
    {
        get => _useDuplicateHashCache;
        set { if (SetProperty(ref _useDuplicateHashCache, value)) { _duplicateHasher.Enabled = value; _ = SaveSettingsAsync(); } }
    }
    public int DuplicateHashParallelism
    {
        get => _duplicateHashParallelism;
        set { if (SetProperty(ref _duplicateHashParallelism, Math.Clamp(value, 1, 4))) { _contentScanner.HashParallelism = _duplicateHashParallelism; _ = SaveSettingsAsync(); } }
    }
    public int VisualDuplicateThreshold
    {
        get => _visualDuplicateThreshold;
        set
        {
            if (SetProperty(ref _visualDuplicateThreshold, Math.Clamp(value, 0, 8)))
            {
                _contentScanner.VisualHammingThreshold = _visualDuplicateThreshold;
                OnPropertyChanged(nameof(VisualDuplicateThresholdHint));
                _ = SaveSettingsAsync();
            }
        }
    }
    public string VisualDuplicateThresholdHint => $"允许最多 {VisualDuplicateThreshold}/64 格亮暗不同（一致率至少 {(64 - VisualDuplicateThreshold) * 100d / 64:0.0}%）。数值越小越严格；不代表同图概率。";
    public bool ShowSimilarityDifferenceGrid
    {
        get => _showSimilarityDifferenceGrid;
        set { if (SetProperty(ref _showSimilarityDifferenceGrid, value)) _ = SaveSettingsAsync(); }
    }
    public bool PromptImportResumeAtStartup
    {
        get => _promptImportResumeAtStartup;
        set { if (SetProperty(ref _promptImportResumeAtStartup, value)) _ = SaveSettingsAsync(); }
    }
    public IRelayCommand ClearDuplicateHashCacheCommand => _clearDuplicateHashCacheCommand ??= new RelayCommand(() =>
    {
        try { _duplicateHasher.Clear(); StatusMessage = "查重缓存已清空，下次扫描将重新计算；照片和导入恢复记录不受影响。"; }
        catch (Exception ex) { StatusMessage = "无法清空查重缓存：" + ex.Message; }
    }, () => !IsBusy);
}
