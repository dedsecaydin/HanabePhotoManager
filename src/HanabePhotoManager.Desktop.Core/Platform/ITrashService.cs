namespace HanabePhotoManager.Desktop.Core.Platform;

public interface ITrashService
{
    Task MoveToTrashAsync(string path, CancellationToken cancellationToken = default);
}
