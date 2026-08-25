using System.IO;

namespace HanabePhotoManager.Core.Albums;

/// <summary>
/// 描述由用户维护的自定义相册；相册内容仍由对应文件夹承载。
/// </summary>
public sealed record CustomAlbum(Guid Id, string DisplayName, string FolderPath)
{
    /// <summary>
    /// 验证并规范化相册名称与文件夹路径；空标识会替换为新的稳定标识。
    /// </summary>
    public static CustomAlbum Create(Guid id, string? displayName, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("A folder path is required.", nameof(folderPath));
        }

        var normalizedPath = Path.GetFullPath(folderPath);
        var normalizedName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : displayName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("An album display name is required.", nameof(displayName));
        }

        return new CustomAlbum(id == Guid.Empty ? Guid.NewGuid() : id, normalizedName, normalizedPath);
    }
}
