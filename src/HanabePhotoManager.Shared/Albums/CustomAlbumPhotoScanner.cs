using System.IO;

namespace HanabePhotoManager.App.Albums;

/// <summary>扫描自定义相册文件夹并返回稳定排序的可浏览照片。</summary>
public sealed class CustomAlbumPhotoScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"],
        StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<CustomAlbumPhoto>> ScanAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        return Task.Run(() => Scan(folderPath, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<CustomAlbumPhoto> Scan(string folderPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        var photos = new List<CustomAlbumPhoto>();
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
                {
                    continue;
                }

                var info = new FileInfo(filePath);
                photos.Add(new CustomAlbumPhoto(info.Name, info.FullName, info.Length));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Return all items reached before a protected nested directory.
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }

        return photos.OrderBy(photo => photo.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}

/// <summary>自定义相册中的照片名称、路径和体积。</summary>
public sealed record CustomAlbumPhoto(string Name, string FullPath, long Length);
