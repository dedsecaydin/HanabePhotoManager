using HanabePhotoManager.Core.Imports;
using System.IO;
using System.Text.Json;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.App.Imports;

internal sealed class OriginMetadataDuplicateMatcher(IFileOriginMetadataStore metadataStore, IFileHasher fileHasher)
{
    private readonly Dictionary<IEnumerable<string>, Task<Dictionary<string, List<Candidate>>>> _indexes
        = new(ReferenceEqualityComparer.Instance);

    internal void BeginBatch() => _indexes.Clear();

    internal async Task<string?> FindMatchAsync(string sourcePath, IEnumerable<string> candidates, CancellationToken cancellationToken)
    {
        var source = new FileInfo(sourcePath);
        if (!source.Exists) return null;

        if (!_indexes.TryGetValue(candidates, out var indexTask))
        {
            indexTask = BuildIndexAsync(candidates, cancellationToken);
            _indexes[candidates] = indexTask;
        }
        var index = await indexTask.ConfigureAwait(false);
        if (!index.TryGetValue(Key(source.Name, source.Length), out var matches)) return null;

        var sourceHash = await fileHasher.ComputeSha256Async(source.FullName, cancellationToken).ConfigureAwait(false);
        foreach (var match in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = match.Path;
            var info = new FileInfo(candidate);
            if (!info.Exists) continue;
            var metadata = match.Metadata;
            if (info.Length != match.Length || info.LastWriteTimeUtc != match.Modified || info.CreationTimeUtc != match.Created)
                metadata = await metadataStore.ReadAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (metadata is not null && metadata.OriginalLength == source.Length &&
                string.Equals(metadata.OriginalName, source.Name, StringComparison.OrdinalIgnoreCase) &&
                !candidate.Equals(source.FullName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(sourceHash, metadata.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string Key(string name, long length) => name + "\0" + length;

    private async Task<Dictionary<string, List<Candidate>>> BuildIndexAsync(
        IEnumerable<string> candidates, CancellationToken token)
    {
        var index = new Dictionary<string, List<Candidate>>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            FileOriginMetadata? metadata;
            try
            {
                if (fileHasher is CachedFileHasher cache)
                {
                    var json = await cache.GetOrComputeAsync(candidate, "origin-metadata-v1", async ct =>
                        JsonSerializer.Serialize(await metadataStore.ReadAsync(candidate, ct).ConfigureAwait(false)), token).ConfigureAwait(false);
                    metadata = JsonSerializer.Deserialize<FileOriginMetadata>(json);
                }
                else metadata = await metadataStore.ReadAsync(candidate, token).ConfigureAwait(false);
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            if (metadata is null) continue;
            var info = new FileInfo(candidate);
            if (!info.Exists) continue;
            var key = Key(metadata.OriginalName, metadata.OriginalLength);
            if (!index.TryGetValue(key, out var list)) index[key] = list = [];
            list.Add(new Candidate(candidate, metadata, info.Length, info.LastWriteTimeUtc, info.CreationTimeUtc));
        }
        return index;
    }

    private sealed record Candidate(string Path, FileOriginMetadata Metadata, long Length, DateTime Modified, DateTime Created);
}
