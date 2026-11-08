using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.Platform;

internal sealed class UnavailableImportSourcePicker : IImportSourcePicker
{
    public IReadOnlyList<string> PickFolders(string initialDirectory) => [];
}

internal sealed class UnavailableStartupRegistration : IStartupRegistrationService
{
    public bool IsEnabled() => false;
    public void SetEnabled(bool enabled) => throw new PlatformNotSupportedException("此平台尚未配置开机启动。");
}

internal sealed class UnavailableWallpaperService : IWindowsWallpaperService
{
    public string? GetCurrentWallpaperPath() => null;
}

internal sealed class UnavailableOriginMetadataStore : IFileOriginMetadataStore
{
    public Task<FileOriginMetadata?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<FileOriginMetadata?>(null);
    }
    public Task<FileOriginMetadataWriteResult> WriteAndVerifyAsync(string path, FileOriginMetadata metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FileOriginMetadataWriteResult.Failure("此平台尚未配置文件来源标记。"));
    }
}

internal sealed class UnavailableThumbnailBackend : IThumbnailBackend
{
    public object? Decode(string path, int width) => null;
    public object? Shell(string path, int width, bool allowExtraction) => null;
}
