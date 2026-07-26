namespace HanabePhotoManager.Desktop.Core.Platform;

public interface IExternalFileService
{
    Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken = default);
}
