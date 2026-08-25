namespace HanabePhotoManager.App.Browsing.Treemap;

using System.Windows.Media;

/// <summary>空间树中可绘制节点的标签、权重、缩略图和层级信息。</summary>
public sealed record TreemapItemViewModel(
    string Key,
    string? ParentKey,
    string Label,
    double Weight,
    bool IsContainer,
    string? FullPath,
    long Length,
    string Category,
    string Extension,
    ImageSource? Thumbnail = null,
    double AspectRatio = 1.0);

/// <summary>空间树当前层级的面包屑项。</summary>
public sealed record TreemapBreadcrumbViewModel(string? Key, string Label);
