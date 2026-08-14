using System.IO;
using System.Runtime.CompilerServices;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.App.Cloud;

/// <summary>
/// Honest no-data-source provider: reports a specific unauthenticated /
/// not-yet-integrated account state instead of fabricating capacity numbers.
/// Used for the Baidu tab while no saved API session exists and for the Quark
/// tab while no Quark connector is implemented.
/// </summary>
internal sealed class UnauthenticatedCloudProvider : ICloudProvider
{
    private readonly CloudAccountState _state;

    public UnauthenticatedCloudProvider(
        CloudProviderKind kind,
        string displayName,
        string statusText)
    {
        _state = new CloudAccountState(kind, false, displayName, 0, 0, statusText);
    }

    public CloudProviderKind Kind => _state.Provider;

    public Task<CloudAccountState> GetAccountStateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_state);

    public async IAsyncEnumerable<CloudObject> ListAsync(
        CloudPath directory,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<Stream?> OpenThumbnailAsync(CloudObject item, CancellationToken cancellationToken) =>
        throw new NotSupportedException("账户未接入，无法读取缩略图。");

    public Task<Stream> OpenReadAsync(CloudObject item, CancellationToken cancellationToken) =>
        throw new NotSupportedException("账户未接入，无法读取内容。");

    public Task<CloudObject> EnsureFolderAsync(CloudPath path, CancellationToken cancellationToken) =>
        throw new NotSupportedException("账户未接入，无法创建目录。");

    public Task<string> UploadAsync(
        string localPath,
        CloudPath destination,
        IProgress<CloudUploadProgress>? progress,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("账户未接入，无法上传。");

    public Task<CloudVerificationResult> VerifyAsync(
        string remoteId,
        CloudTransferFile expected,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("账户未接入，无法校验。");
}
