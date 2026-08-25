using System.Text.Json;
using HanabePhotoManager.Core.Albums;

namespace HanabePhotoManager.Infrastructure.Albums;

/// <summary>
/// 使用 JSON 文件持久化自定义相册，并通过临时文件替换避免写入中断留下半份配置。
/// </summary>
public sealed class JsonCustomAlbumStore : ICustomAlbumStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>创建指向指定配置文件的相册存储。</summary>
    public JsonCustomAlbumStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomAlbum>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var albums = await JsonSerializer.DeserializeAsync<List<CustomAlbum>>(
            stream,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);
        return albums ?? [];
    }

    /// <inheritdoc />
    public async Task SaveAsync(IReadOnlyCollection<CustomAlbum> albums, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(albums);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporaryPath = _filePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, albums, SerializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // 先完整写入同目录临时文件，再替换目标，避免序列化取消时破坏旧配置。
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _gate.Release();
        }
    }
}
