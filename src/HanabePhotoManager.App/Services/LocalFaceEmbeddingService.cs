using System.IO;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

public interface ILocalFaceEmbeddingService
{
    FaceModelIdentity ModelIdentity => FaceModelIdentity.YuNetSFaceLegacy;
    Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken);
    Task<IReadOnlyList<DetectedFace>> DetectBatchAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        DetectBatchFallbackAsync(this, paths, cancellationToken);

    private static async Task<IReadOnlyList<DetectedFace>> DetectBatchFallbackAsync(
        ILocalFaceEmbeddingService service, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var result = new List<DetectedFace>();
        foreach (var path in paths)
            result.AddRange(await service.DetectAsync(path, cancellationToken).ConfigureAwait(false));
        return result;
    }
}

public sealed record DetectedFace(
    string SourcePath,
    float[] Embedding,
    int X,
    int Y,
    int Width,
    int Height,
    float Confidence = 1);

public sealed class LocalFaceEmbeddingService : ILocalFaceEmbeddingService
{
    public FaceModelIdentity ModelIdentity => FaceRecognitionRuntimeOptions.CurrentIdentity;

    public Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken) =>
        FaceRecognitionEngineFactory.Create(FaceRecognitionRuntimeOptions.Current).DetectAsync(path, cancellationToken);

    public Task<IReadOnlyList<DetectedFace>> DetectBatchAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        FaceRecognitionEngineFactory.Create(FaceRecognitionRuntimeOptions.Current).DetectBatchAsync(paths, cancellationToken);
}
