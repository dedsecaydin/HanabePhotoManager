namespace HanabePhotoManager.App.Services;

public static class FaceEmbeddingProvider
{
    public static Func<ILocalFaceEmbeddingService> PlatformFactory { get; set; } = () => new UnavailableFaceEmbeddingService();
    public static ILocalFaceEmbeddingService Create() => PlatformFactory();

    private sealed class UnavailableFaceEmbeddingService : ILocalFaceEmbeddingService
    {
        public Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<DetectedFace>>(new PlatformNotSupportedException("此平台尚未配置人脸识别引擎。"));
    }
}
