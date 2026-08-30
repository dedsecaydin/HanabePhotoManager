using HanabePhotoManager.Core.Imports;
using System.IO;

namespace HanabePhotoManager.App.Imports;

internal sealed class OriginMetadataDuplicateMatcher(IFileOriginMetadataStore metadataStore, IFileHasher fileHasher)
{
    internal async Task<string?> FindMatchAsync(string sourcePath, IEnumerable<string> candidates, CancellationToken cancellationToken)
    {
        var source = new FileInfo(sourcePath);
        if (!source.Exists) return null;

        string? sourceHash = null;
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = await metadataStore.ReadAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (metadata is null
                || !string.Equals(metadata.OriginalName, source.Name, StringComparison.OrdinalIgnoreCase)
                || metadata.OriginalLength != source.Length)
            {
                continue;
            }

            sourceHash ??= await fileHasher.ComputeSha256Async(source.FullName, cancellationToken).ConfigureAwait(false);
            if (string.Equals(sourceHash, metadata.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
}
