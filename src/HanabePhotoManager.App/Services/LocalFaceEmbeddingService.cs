using System.IO;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

/// <summary>封装当前配置的人脸识别引擎并统一图片预处理与错误降级。</summary>
public sealed class LocalFaceEmbeddingService : ILocalFaceEmbeddingService
{
    public FaceModelIdentity ModelIdentity => FaceRecognitionRuntimeOptions.CurrentIdentity;

    public Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken) =>
        FaceRecognitionEngineFactory.Create(FaceRecognitionRuntimeOptions.Current).DetectAsync(path, cancellationToken);

    public Task<IReadOnlyList<DetectedFace>> DetectBatchAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        FaceRecognitionEngineFactory.Create(FaceRecognitionRuntimeOptions.Current).DetectBatchAsync(paths, cancellationToken);
}
