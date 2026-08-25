using System.IO;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

/// <summary>定义本地人脸检测与特征向量提取能力。</summary>
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

/// <summary>检测到的人脸区域、关键点、置信度和归一化特征向量。</summary>
public sealed record DetectedFace(
    string SourcePath,
    float[] Embedding,
    int X,
    int Y,
    int Width,
    int Height,
    float Confidence = 1);

/// <summary>封装当前配置的人脸识别引擎并统一图片预处理与错误降级。</summary>
public sealed class LocalFaceEmbeddingService : ILocalFaceEmbeddingService
{
    public FaceModelIdentity ModelIdentity => FaceRecognitionRuntimeOptions.CurrentIdentity;

    public Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken) =>
        FaceRecognitionEngineFactory.Create(FaceRecognitionRuntimeOptions.Current).DetectAsync(path, cancellationToken);

    public Task<IReadOnlyList<DetectedFace>> DetectBatchAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        FaceRecognitionEngineFactory.Create(FaceRecognitionRuntimeOptions.Current).DetectBatchAsync(paths, cancellationToken);
}
