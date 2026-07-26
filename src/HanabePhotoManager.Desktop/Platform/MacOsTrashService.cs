using HanabePhotoManager.Desktop.Core.Platform;

namespace HanabePhotoManager.Desktop.Platform;

public sealed class MacOsTrashService : ITrashService
{
    private readonly IProcessRunner _processRunner;

    public MacOsTrashService(IProcessRunner processRunner)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("MacOsTrashService can only be used on macOS.");
        }

        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task MoveToTrashAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("The path to move to Trash does not exist.", path);
        }

        await _processRunner.RunAsync(MacOsCommandPolicy.MoveToTrash(path), cancellationToken);
    }
}
