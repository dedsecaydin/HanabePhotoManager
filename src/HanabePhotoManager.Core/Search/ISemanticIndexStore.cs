namespace HanabePhotoManager.Core.Search;

/// <summary>
/// 定义语义索引条目的持久化操作，使搜索领域逻辑不依赖具体数据库或文件格式。
/// </summary>
public interface ISemanticIndexStore
{
    /// <summary>按文件键新增或更新索引条目。</summary>
    Task UpsertAsync(IReadOnlyList<SemanticIndexEntry> entries, CancellationToken cancellationToken);

    /// <summary>读取当前索引中的全部条目。</summary>
    Task<IReadOnlyList<SemanticIndexEntry>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>返回当前持久化的索引条目数。</summary>
    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>移除已不在照片库路径集合中的陈旧索引。</summary>
    Task RemoveMissingAsync(IEnumerable<string> existingPaths, CancellationToken cancellationToken);
}
