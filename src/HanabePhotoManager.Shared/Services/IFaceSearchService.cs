namespace HanabePhotoManager.App.Services;

public interface IFaceSearchService
{
    Task<FaceReference> CreateReferenceAsync(string imagePath, CancellationToken cancellationToken);
    Task<IReadOnlyList<FaceSearchMatch>> SearchAsync(FaceReference reference, string libraryRoot, double minimumSimilarity, IProgress<FaceSearchProgress>? progress, CancellationToken cancellationToken);
}

public sealed record FaceReference(string Path, float[] Embedding);
public sealed record FaceSearchMatch(string Path, double Similarity, int FacesInImage);
public sealed record FaceSearchProgress(int Processed, int Total, int Matches, bool FromCache, string CurrentFile);

public static class FaceSearchProvider
{
    public static Func<IFaceSearchService> PlatformFactory { get; set; } = () => new UnavailableFaceSearchService();
    private sealed class UnavailableFaceSearchService : IFaceSearchService
    {
        public Task<FaceReference> CreateReferenceAsync(string imagePath, CancellationToken cancellationToken) => Task.FromException<FaceReference>(new PlatformNotSupportedException("此平台尚未配置人脸查找。"));
        public Task<IReadOnlyList<FaceSearchMatch>> SearchAsync(FaceReference reference, string libraryRoot, double minimumSimilarity, IProgress<FaceSearchProgress>? progress, CancellationToken cancellationToken) => Task.FromException<IReadOnlyList<FaceSearchMatch>>(new PlatformNotSupportedException("此平台尚未配置人脸查找。"));
    }
}
