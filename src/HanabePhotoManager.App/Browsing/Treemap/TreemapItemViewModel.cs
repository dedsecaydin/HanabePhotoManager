namespace HanabePhotoManager.App.Browsing.Treemap;

public sealed record TreemapItemViewModel(
    string Key,
    string? ParentKey,
    string Label,
    double Weight,
    bool IsContainer,
    string? FullPath,
    long Length,
    string Category,
    string Extension);

public sealed record TreemapBreadcrumbViewModel(string? Key, string Label);

