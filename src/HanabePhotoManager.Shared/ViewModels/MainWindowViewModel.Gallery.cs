using CommunityToolkit.Mvvm.Input;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>
/// 照片图库：筛选常驻在照片墙上方（不再藏在“浏览条件”折叠里），左侧是按月分组的日期列表，右侧信息面板可开关（Ctrl+I）。
/// </summary>
public sealed partial class MainWindowViewModel
{
    private bool _isBrowseInspectorOpen = true;

    /// <summary>类型分段：全部 / RAW / JPG / 修后 / 视频 / 运动相机 / 素材（显示名与筛选值一一对应）。</summary>
    public IReadOnlyList<PreviewCategoryChip> PreviewCategoryChips { get; } =
    [
        new("全部", "全部"),
        new("RAW生图", "RAW"),
        new("JPG生图", "JPG"),
        new("修后", "修后"),
        new("视频", "视频"),
        new("action视频", "运动相机"),
        new("素材", "素材"),
    ];

    /// <summary>左侧日期列表点一天：只看这一天。</summary>
    [RelayCommand]
    private void SelectLibraryDate(LibraryDateNode? node)
    {
        if (node?.Date is null || IsBusy) return;
        SelectedDate = node;
    }

    /// <summary>右侧信息面板是否打开（Ctrl+I 切换）。</summary>
    public bool IsBrowseInspectorOpen
    {
        get => _isBrowseInspectorOpen;
        set => SetProperty(ref _isBrowseInspectorOpen, value);
    }

    [RelayCommand]
    private void ToggleBrowseInspector() => IsBrowseInspectorOpen = !IsBrowseInspectorOpen;

    /// <summary>有任何一项筛选不是默认值：工具栏显示“清除筛选”。</summary>
    public bool HasActiveBrowseFilters =>
        SemanticSearch.HasActiveQuery ||
        CurrentPreviewCategory != "全部" ||
        !string.IsNullOrWhiteSpace(UnifiedSearchText) ||
        PreviewRetouchFilter != "全部" ||
        SmartCategoryFilter != "全部智能类别" ||
        RatingFilter != "全部评分" ||
        PeopleAlbums.SelectedAlbum is not null;

    /// <summary>左侧日期列表“全部日期”是否为当前项。</summary>
    public bool IsAllDatesSelected => SelectedDate is null;

    /// <summary>当前选中日期的路径（左侧日期列表用来标出当前项）。</summary>
    public string GalleryCurrentDatePath => SelectedDate?.SelectionKey ?? string.Empty;

    /// <summary>标题旁的当前范围：“全部日期 · 35 张”或“10.21_婚礼 · 30 张”。</summary>
    public string GalleryScopeText =>
        $"{(SelectedDate is null ? "全部日期" : SelectedDate.ShortTitle)} · {CurrentViewItemCount:N0} 张";

    /// <summary>筛选或日期变化时（这些地方都会通知 BrowseConditionsSummary / SelectedDate），顺带刷新图库工具栏状态。</summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(AutoSplitImportDays) or nameof(ImportDaySplitHours))
            OnDaySessionSettingsChanged();
        if (e.PropertyName is nameof(BrowseConditionsSummary) or nameof(SelectedDate) or nameof(SelectedDateTitle) or nameof(UnifiedSearchText)
            or nameof(CurrentPreviewCategory) or nameof(PreviewRetouchFilter) or nameof(RatingFilter) or nameof(SmartCategoryFilter))
            NotifyGalleryChromeChanged();
        if (e.PropertyName is nameof(CurrentViewItemCount) or nameof(FilteredPreviewCount) or nameof(SelectedDate) or nameof(BrowseConditionsSummary))
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(GalleryScopeText)));
    }

    private void NotifyGalleryChromeChanged()
    {
        OnPropertyChanged(nameof(HasActiveBrowseFilters));
        OnPropertyChanged(nameof(IsAllDatesSelected));
        OnPropertyChanged(nameof(GalleryCurrentDatePath));
    }
}

/// <summary>图库类型分段的一项：Value 是筛选值，Label 是显示名。</summary>
public sealed record PreviewCategoryChip(string Value, string Label);
