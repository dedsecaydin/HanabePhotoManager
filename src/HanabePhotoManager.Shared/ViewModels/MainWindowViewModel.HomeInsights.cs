using System.Collections.ObjectModel;
using System.IO;
using HanabePhotoManager.App.Platform;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>主页“交付进度”的一个拍摄日：原片数量与“修后”文件夹里的成片数量。</summary>
public sealed record HomeDeliveryItemViewModel(LibraryDateNode Node, int OriginalCount, int RetouchedCount)
{
    public string DateText => Node.Date is { } date ? $"{date.Month}.{date.Day:00}" : Node.Title;

    /// <summary>日期文件夹的备注（“11.04_婚礼” → “婚礼”），没有备注时显示原片数。</summary>
    public string Remark
    {
        get
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Node.FullPath));
            var underscore = name.IndexOf('_');
            return underscore >= 0 && underscore < name.Length - 1 ? name[(underscore + 1)..] : $"{OriginalCount:N0} 张原片";
        }
    }

    public bool IsDelivered => RetouchedCount > 0;

    public string StatusText => IsDelivered ? $"已修 {RetouchedCount:N0}" : "待修图";

    public string ToolTipText => $"{Path.GetFileName(Node.FullPath)}\n原片 {OriginalCount:N0} · 修后 {RetouchedCount:N0}\n点击在图库中打开";
}

public sealed partial class MainWindowViewModel
{
    private const int HomeDeliveryLimit = 5;
    private IAsyncRelayCommand<HomeDeliveryItemViewModel>? _openHomeDeliveryCommand;
    private double _shootingBarMaxHeight = ShootingFrequencyMaxBarHeight;
    private string _libraryStorageText = "正在读取照片库所在磁盘…";
    private string _libraryStorageDetail = string.Empty;
    private double _libraryStorageFraction;
    private bool _isLibraryStorageLow;
    private int _storageRefreshVersion;

    /// <summary>最近几个拍摄日的修图进度：有没有出“修后”成片。</summary>
    public ObservableCollection<HomeDeliveryItemViewModel> HomeDeliveryItems { get; } = [];

    public string HomeDeliverySummary
    {
        get
        {
            if (HomeDeliveryItems.Count == 0) return "导入后显示每个拍摄日的修图进度";
            var pending = HomeDeliveryItems.Count(item => !item.IsDelivered);
            return pending == 0 ? "最近的拍摄都已出修后" : $"{pending} 个拍摄日待修图";
        }
    }

    public bool HasHomeDelivery => HomeDeliveryItems.Count > 0;

    public IAsyncRelayCommand<HomeDeliveryItemViewModel> OpenHomeDeliveryCommand =>
        _openHomeDeliveryCommand ??= new AsyncRelayCommand<HomeDeliveryItemViewModel>(async item =>
        {
            if (item is null || !Directory.Exists(item.Node.FullPath)) return;
            CurrentPage = "Preview";
            await SelectDateAsync(item.Node).ConfigureAwait(true);
        });

    /// <summary>拍摄频率条形图的最大高度，跟随主页板块高度变化，板块被拉高时条形也一起变高。</summary>
    public double ShootingBarMaxHeight
    {
        get => _shootingBarMaxHeight;
        set
        {
            var clamped = Math.Clamp(Math.Round(value), 48, 360);
            if (Math.Abs(clamped - _shootingBarMaxHeight) < 4) return;
            _shootingBarMaxHeight = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShootingFrequencyBars));
        }
    }

    private void RebuildHomeDelivery(IReadOnlyList<PreviewFileViewModel> files)
    {
        var retouchedFolder = ConcreteCategoryFolders.TryGetValue(MediaCategory.Edited, out var name) ? name : "修后";
        var nodes = FlattenDateNodes(LibraryDates)
            .Where(node => node.Date is not null)
            .OrderByDescending(node => node.Date!.Value.Year)
            .ThenByDescending(node => node.Date!.Value.Month)
            .ThenByDescending(node => node.Date!.Value.Day)
            .Take(HomeDeliveryLimit * 3)
            .ToArray();

        var items = new List<HomeDeliveryItemViewModel>();
        foreach (var node in nodes)
        {
            var prefix = Path.TrimEndingDirectorySeparator(node.FullPath) + Path.DirectorySeparatorChar;
            var retouchedPrefix = prefix + retouchedFolder + Path.DirectorySeparatorChar;
            int originals = 0, retouched = 0;
            foreach (var file in files)
            {
                if (!file.FullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (file.FullPath.StartsWith(retouchedPrefix, StringComparison.OrdinalIgnoreCase)) retouched++;
                else originals++;
            }
            // 统计口径与目录树一致；还没读到文件时退回目录树已有的统计。
            if (originals + retouched == 0)
            {
                originals = Math.Max(0, node.TotalFiles - node.RetouchedFiles);
                retouched = node.RetouchedFiles;
            }
            if (originals + retouched == 0) continue;
            items.Add(new HomeDeliveryItemViewModel(node, originals, retouched));
            if (items.Count == HomeDeliveryLimit) break;
        }

        HomeDeliveryItems.Clear();
        foreach (var item in items) HomeDeliveryItems.Add(item);
        OnPropertyChanged(nameof(HomeDeliverySummary));
        OnPropertyChanged(nameof(HasHomeDelivery));
    }

    // ---------- 存储空间 ----------

    public string LibraryStorageText { get => _libraryStorageText; private set => SetProperty(ref _libraryStorageText, value); }
    public string LibraryStorageDetail { get => _libraryStorageDetail; private set => SetProperty(ref _libraryStorageDetail, value); }

    /// <summary>已用比例 0–1，用于容量条。</summary>
    public double LibraryStorageFraction { get => _libraryStorageFraction; private set => SetProperty(ref _libraryStorageFraction, value); }

    /// <summary>剩余不足 10% 或 50 GB 时提醒（一场婚礼的 RAW + 视频就可能上百 GB）。</summary>
    public bool IsLibraryStorageLow { get => _isLibraryStorageLow; private set => SetProperty(ref _isLibraryStorageLow, value); }

    /// <summary>读取照片库所在磁盘（含网络共享）的容量；网络盘响应慢时 4 秒后放弃，不阻塞界面。</summary>
    public async Task RefreshLibraryStorageAsync()
    {
        var version = ++_storageRefreshVersion;
        var root = LibraryRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            LibraryStorageText = "还没有选择照片库";
            LibraryStorageDetail = string.Empty;
            LibraryStorageFraction = 0;
            IsLibraryStorageLow = false;
            return;
        }

        var query = Task.Run(() => PlatformServices.StorageCapacity.GetCapacity(root));
        var finished = await Task.WhenAny(query, Task.Delay(TimeSpan.FromSeconds(4))).ConfigureAwait(true);
        if (version != _storageRefreshVersion) return;
        if (finished != query || query.Result is not { } space)
        {
            LibraryStorageText = "暂时读不到照片库所在磁盘";
            LibraryStorageDetail = "网络共享未连接或没有权限时会这样，连上后点“刷新”。";
            LibraryStorageFraction = 0;
            IsLibraryStorageLow = false;
            return;
        }

        var used = space.Total - space.Free;
        LibraryStorageFraction = (double)used / space.Total;
        LibraryStorageText = $"剩余 {FormatBytes((long)space.Free)} / 共 {FormatBytes((long)space.Total)}";
        IsLibraryStorageLow = space.Free < space.Total / 10 || space.Free < 50UL * 1024 * 1024 * 1024;
        LibraryStorageDetail = IsLibraryStorageLow
            ? "空间不多了：下一场拍摄导入前，记得先归档或清理。"
            : $"已用 {LibraryStorageFraction:P0} · {root}";
    }
}
