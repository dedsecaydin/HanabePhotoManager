namespace HanabePhotoManager.App.Browsing.Treemap;

using System.Windows.Media;

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

public sealed record TreemapBreadcrumbViewModel(string? Key, string Label);
