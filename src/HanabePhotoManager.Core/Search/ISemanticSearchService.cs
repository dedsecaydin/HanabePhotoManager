namespace HanabePhotoManager.Core.Search;

public interface ISemanticSearchService
{
    Task EnsureIndexAsync(
        string libraryRoot,
        IProgress<SemanticIndexStatus>? progress,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);

    SemanticIndexStatus GetIndexStatus();
}
