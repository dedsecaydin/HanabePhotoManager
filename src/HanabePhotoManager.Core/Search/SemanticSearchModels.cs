namespace HanabePhotoManager.Core.Search;

/// <summary>一次语义搜索请求及其最大返回数量。</summary>
public sealed record SemanticSearchQuery(string Text, int Limit = 50);

/// <summary>语义搜索命中的媒体键与归一化相关度。</summary>
public sealed record SemanticSearchResult(string FileKey, double Score);

/// <summary>语义索引构建过程的不可变状态快照。</summary>
public sealed record SemanticIndexStatus(
    int TotalFiles,
    int IndexedFiles,
    bool IsIndexing,
    bool IsModelReady,
    string Message)
{
    /// <summary>已索引文件占全部待索引文件的百分比。</summary>
    public double ProgressPercent => TotalFiles == 0 ? 0 : IndexedFiles * 100d / TotalFiles;
}

/// <summary>用于增量检测和向量检索的单个媒体索引条目。</summary>
public sealed record SemanticIndexEntry(
    string FileKey,
    string Fingerprint,
    DateTimeOffset ModifiedAtUtc,
    IReadOnlyList<float> Embedding);
