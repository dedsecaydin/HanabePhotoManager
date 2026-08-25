namespace HanabePhotoManager.Core.Imports;

/// <summary>定义文件内容 SHA-256 计算边界。</summary>
public interface IFileHasher
{
    /// <summary>异步读取文件并返回大写十六进制 SHA-256。</summary>
    Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken);
}

/// <summary>在生成导入计划时检查目标路径是否已存在相同或冲突内容。</summary>
public interface IDestinationProbe
{
    /// <summary>比较源文件与指定目标路径并返回冲突种类。</summary>
    Task<ConflictKind> CheckAsync(SourceMediaFile source, string destination, CancellationToken cancellationToken);
}
