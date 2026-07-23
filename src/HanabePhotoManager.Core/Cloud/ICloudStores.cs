namespace HanabePhotoManager.Core.Cloud;

public interface ICloudTransferQueueStore
{
    Task<IReadOnlyList<CloudTransferJob>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyCollection<CloudTransferJob> jobs,
        CancellationToken cancellationToken = default);
}

public interface ICloudIndexStore
{
    Task UpsertAsync(
        IEnumerable<CloudObject> items,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CloudObject>> QueryChildrenAsync(
        CloudProviderKind provider,
        CloudPath directory,
        CancellationToken cancellationToken = default);

    Task RemoveProviderAsync(
        CloudProviderKind provider,
        CancellationToken cancellationToken = default);
}

public interface ICloudCacheStore
{
    Task<string?> TryGetAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores cache content by reading from the stream's current position through its end.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="content">
    /// A readable stream positioned at the first byte to cache. Ownership remains with the caller;
    /// the store must not dispose the stream.
    /// </param>
    /// <param name="pinned">Whether trimming should preserve the cached content.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The local path of the cached content.</returns>
    Task<string> PutAsync(
        string key,
        Stream content,
        bool pinned,
        CancellationToken cancellationToken = default);

    Task TrimAsync(
        long maximumBytes,
        CancellationToken cancellationToken = default);
}
