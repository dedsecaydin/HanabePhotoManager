namespace HanabePhotoManager.Core.Search;

/// <summary>
/// 提供与具体向量模型无关的照片语义索引和查询能力。
/// </summary>
public interface ISemanticSearchService
{
    /// <summary>确保指定照片库已有可查询的最新索引。</summary>
    Task EnsureIndexAsync(
        string libraryRoot,
        IProgress<SemanticIndexStatus>? progress,
        CancellationToken cancellationToken);

    /// <summary>按自然语言查询返回相关度从高到低的媒体结果。</summary>
    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>读取最近一次索引任务的同步状态快照。</summary>
    SemanticIndexStatus GetIndexStatus();
}
