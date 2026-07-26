namespace HanabePhotoManager.Desktop.Core.Platform;

public interface IProcessRunner
{
    Task<int> RunAsync(ProcessCommand command, CancellationToken cancellationToken);
}
