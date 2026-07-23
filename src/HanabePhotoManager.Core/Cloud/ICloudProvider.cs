namespace HanabePhotoManager.Core.Cloud;

public interface ICloudProvider
{
    CloudProviderKind Kind { get; }

    Task<CloudAccountState> GetAccountStateAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<CloudObject> ListAsync(
        CloudPath directory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens thumbnail content when it is available.
    /// </summary>
    /// <returns>
    /// A readable stream owned by the caller, who must dispose it, or <see langword="null"/> when no thumbnail exists.
    /// </returns>
    Task<Stream?> OpenThumbnailAsync(
        CloudObject item,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens the object's content for reading.
    /// </summary>
    /// <returns>A readable stream owned by the caller, who must dispose it.</returns>
    Task<Stream> OpenReadAsync(
        CloudObject item,
        CancellationToken cancellationToken);

    Task<CloudObject> EnsureFolderAsync(
        CloudPath path,
        CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a local file to a complete remote file path.
    /// </summary>
    /// <param name="localPath">The local source file path.</param>
    /// <param name="destination">
    /// The complete remote target file path, including its file name; this is not a directory path.
    /// </param>
    /// <param name="progress">
    /// An optional receiver for cumulative progress for <paramref name="localPath"/>. Each update reports
    /// a nonnegative byte count no greater than the file's total byte count.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the upload.</param>
    /// <returns>The provider's remote identifier for the uploaded file.</returns>
    Task<string> UploadAsync(
        string localPath,
        CloudPath destination,
        IProgress<CloudUploadProgress>? progress,
        CancellationToken cancellationToken);

    Task<CloudVerificationResult> VerifyAsync(
        string remoteId,
        CloudTransferFile expected,
        CancellationToken cancellationToken);
}
