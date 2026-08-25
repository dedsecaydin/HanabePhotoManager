using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>
/// 先按存在性和长度快速筛选，再以 SHA-256 确认目标文件是否与源文件完全相同。
/// </summary>
public sealed class DestinationProbe(IFileHasher fileHasher) : IDestinationProbe
{
    private readonly IFileHasher _fileHasher = fileHasher ?? throw new ArgumentNullException(nameof(fileHasher));

    /// <inheritdoc />
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
