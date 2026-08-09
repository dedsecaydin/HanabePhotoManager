namespace HanabePhotoManager.Core.Search;

public sealed record SemanticSearchQuery(string Text, int Limit = 50);

public sealed record SemanticSearchResult(string FileKey, double Score);

public sealed record SemanticIndexStatus(
    int TotalFiles,
    int IndexedFiles,
    bool IsIndexing,
    bool IsModelReady,
    string Message)
{
    public double ProgressPercent => TotalFiles == 0 ? 0 : IndexedFiles * 100d / TotalFiles;
}

public sealed record SemanticIndexEntry(
    string FileKey,
    string Fingerprint,
    DateTimeOffset ModifiedAtUtc,
    IReadOnlyList<float> Embedding);
