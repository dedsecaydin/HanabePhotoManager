namespace HanabePhotoManager.Core.Albums;

/// <summary>
/// 定义自定义相册配置的持久化边界，不约束具体存储格式。
/// </summary>
public interface ICustomAlbumStore
{
    /// <summary>读取当前保存的全部自定义相册。</summary>
    Task<IReadOnlyList<CustomAlbum>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>以给定集合替换已保存的自定义相册配置。</summary>
    Task SaveAsync(IReadOnlyCollection<CustomAlbum> albums, CancellationToken cancellationToken = default);
}
