namespace HanabePhotoManager.Core.Performance;

/// <summary>
/// 集中维护预览扫描、分批派发、缩略图并发与缓存容量等性能基线。
/// </summary>
public static class PreviewLoadingPolicy
{
    public const int ScanBatchSize = 64;
    public const int VisiblePageSize = 180;
    public const int HomeRecentItemLimit = 24;
    public const int ThumbnailConcurrency = 4;
    public const int ThumbnailCacheLimit = 256;

    /// <summary>计算指定项目数需要拆成多少个 UI Dispatcher 批次。</summary>
    public static int DispatcherBatchCount(int itemCount) =>
        itemCount <= 0 ? 0 : (itemCount + ScanBatchSize - 1) / ScanBatchSize;
}
