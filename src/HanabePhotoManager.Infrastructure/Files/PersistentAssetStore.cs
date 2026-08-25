namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>
/// 管理壁纸、模型或其他用户选择资源的应用私有副本，并隐藏临时写入文件。
/// </summary>
public sealed class PersistentAssetStore
{
    private readonly string _managedDirectory;

    /// <summary>创建使用指定托管目录的资源存储。</summary>
    public PersistentAssetStore(string managedDirectory)
    {
        if (string.IsNullOrWhiteSpace(managedDirectory))
        {
            throw new ArgumentException("Managed directory is required.", nameof(managedDirectory));
        }

        _managedDirectory = Path.GetFullPath(managedDirectory);
    }

    /// <summary>复制资源到托管目录，并通过临时文件替换同名旧资源。</summary>
    public string Import(string sourcePath, string assetName)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected asset does not exist.", sourcePath);
        }

        if (string.IsNullOrWhiteSpace(assetName) ||
            assetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Asset name is invalid.", nameof(assetName));
        }

        Directory.CreateDirectory(_managedDirectory);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var destinationPath = Path.Combine(_managedDirectory, assetName + extension);
        if (string.Equals(Path.GetFullPath(sourcePath), destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return destinationPath;
        }

        var temporaryPath = Path.Combine(
            _managedDirectory,
            $"{assetName}.{Guid.NewGuid():N}.tmp{extension}");
        try
        {
            File.Copy(sourcePath, temporaryPath, overwrite: true);
            File.Move(temporaryPath, destinationPath, overwrite: true);
            return destinationPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>查找指定逻辑名称最新的非临时资源。</summary>
    public string? Find(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName) ||
            assetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Asset name is invalid.", nameof(assetName));
        }

        if (!Directory.Exists(_managedDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(_managedDirectory, assetName + ".*", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).Contains(".tmp", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    /// <summary>删除指定逻辑名称的全部扩展名变体。</summary>
    public void Delete(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName) ||
            assetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Asset name is invalid.", nameof(assetName));
        }

        if (!Directory.Exists(_managedDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(
                     _managedDirectory,
                     assetName + ".*",
                     SearchOption.TopDirectoryOnly))
        {
            File.Delete(path);
        }
    }
}
