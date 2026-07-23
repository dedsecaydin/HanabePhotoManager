using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Infrastructure.Files;

public sealed class DestinationProbe(IFileHasher fileHasher) : IDestinationProbe
{
    private readonly IFileHasher _fileHasher = fileHasher ?? throw new ArgumentNullException(nameof(fileHasher));

    public async Task<ConflictKind> CheckAsync(
        SourceMediaFile source,
        string destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (string.IsNullOrWhiteSpace(source.FullPath))
        {
            throw new ArgumentException("Source FullPath cannot be null or whitespace.", nameof(source));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(destination))
        {
            return ConflictKind.SameNameDifferentContent;
        }

        if (!File.Exists(destination))
        {
            return ConflictKind.None;
        }

        var destinationInfo = new FileInfo(destination);
        if (destinationInfo.Length != source.Length)
        {
            return ConflictKind.SameNameDifferentContent;
        }

        var sourceHash = await _fileHasher
            .ComputeSha256Async(source.FullPath, cancellationToken)
            .ConfigureAwait(false);
        var destinationHash = await _fileHasher
            .ComputeSha256Async(destination, cancellationToken)
            .ConfigureAwait(false);

        return string.Equals(sourceHash, destinationHash, StringComparison.OrdinalIgnoreCase)
            ? ConflictKind.Identical
            : ConflictKind.SameNameDifferentContent;
    }
}
