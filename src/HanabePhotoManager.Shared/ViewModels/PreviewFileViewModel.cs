using CommunityToolkit.Mvvm.ComponentModel;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>图库单个媒体卡片的路径、缩略图、分类、评分和选择状态。</summary>
public sealed partial class PreviewFileViewModel : ObservableObject
{
    public string Name { get; init; }
    public string Category { get; init; }
    public string FullPath { get; init; }
    public string SizeText { get; init; }
    public string Extension { get; init; }
    [ObservableProperty] private object? _thumbnail;

    /// <summary>
    /// Resolution (decode pixel width) of the currently loaded thumbnail. Used
    /// by the zoomable grid to decide whether a sharper copy is needed.
    /// </summary>
    public int LoadedThumbnailDecodeWidth { get; set; }

    /// <summary>
    /// 展开日期分组后新加入照片墙的瓷砖置为 true，用于触发一次「展开弹出」进入动画；
    /// 动画播放后由 code-behind 复位，避免滚动进入视口时重复播放。
    /// </summary>
    public bool RevealOnLoad { get; set; }

    /// <summary>
    /// 展开动画的逐项延迟（毫秒），用于让照片沿标题区域向下依次 reveal。
    /// </summary>
    public int RevealDelayMs { get; set; }

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private bool _hasXmlSidecar;

    [ObservableProperty] private string _smartCategory = "待分类";

    [ObservableProperty] private string _manualTagsDisplay = string.Empty;

    public bool HasManualTags => !string.IsNullOrWhiteSpace(ManualTagsDisplay);

    partial void OnManualTagsDisplayChanged(string value) => OnPropertyChanged(nameof(HasManualTags));

    public PreviewFileViewModel(string name, string category, string fullPath, string sizeText, string extension, object? thumbnail, long length = 0, DateTime capturedAt = default)
    {
        Name = name;
        Category = category;
        FullPath = fullPath;
        SizeText = sizeText;
        Extension = extension;
        Thumbnail = thumbnail;
        Length = length;
        CapturedAt = capturedAt == default ? DateTime.MinValue : capturedAt;
    }

    public long Length { get; }

    /// <summary>
    /// Capture time used for time-based sorting (拍摄时间从新到旧/从旧到新).
    /// Filled from the file's last-write time at scan time so sorting a large
    /// library never triggers per-item file IO. EXIF DateTimeOriginal is not
    /// cached anywhere in the metadata store, so per-file EXIF reads are not
    /// viable for full-library sorts (13k+ files over SMB); the library's
    /// date-folder layout keeps file mtime a faithful capture proxy.
    /// </summary>
    public DateTime CapturedAt { get; init; }

    public string Caption => IsRetouched
        ? $"{Category} · 显示修后成品"
        : $"{Category} · {SizeText}";

    public bool HasThumbnail => Thumbnail is not null;

    public bool HasNoThumbnail => !HasThumbnail;

    partial void OnThumbnailChanged(object? value)
    {
        OnPropertyChanged(nameof(HasThumbnail));
        OnPropertyChanged(nameof(HasNoThumbnail));
    }

    [ObservableProperty] private bool _isRetouched;

    [ObservableProperty] private string? _retouchedPath;

    public string PreviewPath => IsRetouched && !string.IsNullOrWhiteSpace(RetouchedPath)
        ? RetouchedPath
        : FullPath;

    partial void OnIsRetouchedChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewPath));
        OnPropertyChanged(nameof(Caption));
    }

    partial void OnRetouchedPathChanged(string? value) => OnPropertyChanged(nameof(PreviewPath));

    public int Rating
    {
        get => FileMetaStore.TryGet(FullPath).Rating;
        set => FileMetaStore.Update(FullPath, m => m.Rating = Math.Clamp(value, 0, 5));
    }

    public string Tags
    {
        get => FileMetaStore.TryGet(FullPath).Tags;
        set => FileMetaStore.Update(FullPath, m => m.Tags = value ?? "");
    }
}

