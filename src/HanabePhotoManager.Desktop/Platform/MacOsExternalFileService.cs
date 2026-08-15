using HanabePhotoManager.Desktop.Core.Platform;

namespace HanabePhotoManager.Desktop.Platform;

public sealed class MacOsExternalFileService : IExternalFileService
{
    private readonly IProcessRunner _processRunner;

    public MacOsExternalFileService(IProcessRunner processRunner)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("MacOsExternalFileService can only be used on macOS.");
        }

        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("The path to reveal does not exist.", path);
        }

        await _processRunner.RunAsync(MacOsCommandPolicy.Reveal(path), cancellationToken);
    }
}
