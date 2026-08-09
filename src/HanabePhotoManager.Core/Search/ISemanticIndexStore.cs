namespace HanabePhotoManager.Core.Search;

public interface ISemanticIndexStore
{
    Task UpsertAsync(IReadOnlyList<SemanticIndexEntry> entries, CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticIndexEntry>> GetAllAsync(CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    Task RemoveMissingAsync(IEnumerable<string> existingPaths, CancellationToken cancellationToken);
}
