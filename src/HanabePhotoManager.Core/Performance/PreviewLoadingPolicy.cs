namespace HanabePhotoManager.Core.Performance;

public static class PreviewLoadingPolicy
{
    public const int ScanBatchSize = 64;
    public const int VisiblePageSize = 180;
    public const int HomeRecentItemLimit = 24;
    public const int ThumbnailConcurrency = 4;
    public const int ThumbnailCacheLimit = 256;

    public static int DispatcherBatchCount(int itemCount) =>
        itemCount <= 0 ? 0 : (itemCount + ScanBatchSize - 1) / ScanBatchSize;
}
