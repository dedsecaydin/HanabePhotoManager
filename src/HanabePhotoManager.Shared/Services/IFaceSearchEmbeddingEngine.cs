namespace HanabePhotoManager.App.Services;

public interface IFaceSearchEmbeddingEngine : IDisposable
{
    string CacheIdentity { get; }
    IReadOnlyList<float[]> ExtractEmbeddings(string path, CancellationToken cancellationToken);
}

public sealed class PortableFaceSearchEngine(ILocalFaceEmbeddingService engine) : IFaceSearchEmbeddingEngine
{
    public string CacheIdentity => engine.ModelIdentity.StorageKey;
    public IReadOnlyList<float[]> ExtractEmbeddings(string path, CancellationToken cancellationToken) =>
        engine.DetectAsync(path, cancellationToken).GetAwaiter().GetResult()
            .OrderByDescending(face => (long)face.Width * face.Height).Select(face => face.Embedding).ToArray();
    public void Dispose() { }
}
