using System.Collections.Concurrent;
using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

/// <summary>验证模型文件并创建当前配置的人脸识别引擎。</summary>
internal static class LegacyFaceRecognitionEngineFactory
{
    private static readonly ConcurrentDictionary<string, OnnxFaceRecognitionEngine> Engines = new(StringComparer.Ordinal);

    public static ILocalFaceEmbeddingService Create(FaceRecognitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var availability = options.EvaluateAvailability();
        if (!availability.IsAvailable)
            throw new InvalidOperationException(availability.Reason);

        var modelRoot = Path.Combine(AppContext.BaseDirectory, "Models", "Face");
        var detectorPath = options.Engine == FaceRecognitionEngineKind.YuNetSFace
            ? Path.Combine(modelRoot, "face_detection_yunet_2023mar.onnx")
            : options.DetectorModelPath!;
        var recognizerPath = options.Engine == FaceRecognitionEngineKind.YuNetSFace
            ? Path.Combine(modelRoot, "face_recognition_sface_2021dec.onnx")
            : options.RecognizerModelPath!;
        var identity = options.Engine == FaceRecognitionEngineKind.YuNetSFace
            ? FaceModelIdentity.YuNetSFaceCurrent
            : FaceModelIdentity.CreateArcFace(detectorPath, recognizerPath,
                options.MatchThreshold <= 0 ? FaceRecognitionDefaults.ArcFaceR100Threshold : options.MatchThreshold);
        var limits = FaceRuntimeLimits.For(options.Profile);
        var key = $"{identity.StorageKey}:{options.Profile}:{options.MaxConcurrency}:{options.BatchSize}";
        return Engines.GetOrAdd(key, _ => new OnnxFaceRecognitionEngine(
            detectorPath, recognizerPath, identity, limits,
            options.MaxConcurrency > 0 ? options.MaxConcurrency : limits.MaxConcurrency,
            options.BatchSize > 0 ? options.BatchSize : limits.BatchSize));
    }
}

/// <summary>组合 YuNet 检测器与 ArcFace 编码器的本地 ONNX 人脸引擎。</summary>
internal sealed class OnnxFaceRecognitionEngine : ILocalFaceEmbeddingService
{
    private static readonly ConcurrentDictionary<string, Lazy<InferenceSession>> Sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly InferenceSession _detector;
    private readonly InferenceSession _recognizer;
    private readonly FaceRuntimeLimits _limits;
    private readonly int _maxConcurrency;
    private readonly int _batchSize;

    internal OnnxFaceRecognitionEngine(
        string detectorPath, string recognizerPath, FaceModelIdentity identity,
        FaceRuntimeLimits limits, int maxConcurrency, int batchSize)
    {
        if (!File.Exists(detectorPath)) throw new FileNotFoundException("人脸检测器模型缺失。", detectorPath);
        if (!File.Exists(recognizerPath)) throw new FileNotFoundException("人脸识别模型缺失。", recognizerPath);
        ModelIdentity = identity;
        _limits = limits;
        _maxConcurrency = Math.Max(1, maxConcurrency);
        _batchSize = Math.Max(1, batchSize);
        _detector = GetSession(detectorPath);
        _recognizer = GetSession(recognizerPath);
    }

    public FaceModelIdentity ModelIdentity { get; }

    public async Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken)
    {
        var batches = await DetectBatchAsync([path], cancellationToken).ConfigureAwait(false);
        return batches;
    }

    public async Task<IReadOnlyList<DetectedFace>> DetectBatchAsync(
        IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var nested = await FaceBatchExecutor.RunAsync(
            paths, _maxConcurrency, _batchSize, DetectCoreAsync, cancellationToken).ConfigureAwait(false);
        return nested.SelectMany(static faces => faces).ToArray();
    }

    private Task<IReadOnlyList<DetectedFace>> DetectCoreAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() => DetectCore(path, cancellationToken), cancellationToken);

    private IReadOnlyList<DetectedFace> DetectCore(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = Cv2.ImRead(path, ImreadModes.Color);
        if (source.Empty()) return [];
        var detectorMetadata = _detector.InputMetadata.First();
        var declaredDimensions = detectorMetadata.Value.Dimensions;
        var declaredEdge = declaredDimensions.Length >= 4 ? declaredDimensions[^1] : -1;
        var edge = declaredEdge > 0 ? declaredEdge : _limits.MaximumImageEdge;
        var scaleX = source.Width / (float)edge;
        var scaleY = source.Height / (float)edge;
        using var resized = new Mat();
        Cv2.Resize(source, resized, new CvSize(edge, edge), 0, 0, InterpolationFlags.Area);
        var input = ToTensor(resized, normalize: false, divisor: 1);
        var inputName = detectorMetadata.Key;
        using var outputs = _detector.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
        var detections = YuNetDecoder.Decode(outputs, edge, .6f, .3f);
        var result = new List<DetectedFace>(detections.Count);
        foreach (var detection in detections.Take(32))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var detectedWidth = (int)Math.Round(detection.Width * scaleX);
            var detectedHeight = (int)Math.Round(detection.Height * scaleY);
            if (!FaceRecognitionMath.IsAcceptableFaceDetection(
                    detection.Score, detectedWidth, detectedHeight))
                continue;
            var landmarks = detection.Landmarks
                .Select(point => new Point2f(point.X * scaleX, point.Y * scaleY)).ToArray();
            using var aligned = Align(source, landmarks);
            var tensor = ToRecognitionTensor(aligned, ModelIdentity.Engine);
            var recognitionInput = _recognizer.InputMetadata.Keys.First();
            using var embeddingOutput = _recognizer.Run([NamedOnnxValue.CreateFromTensor(recognitionInput, tensor)]);
            var embedding = embeddingOutput.First().AsEnumerable<float>().ToArray();
            FaceRecognitionMath.L2Normalize(embedding);
            result.Add(new(path, embedding,
                (int)Math.Round(detection.X * scaleX), (int)Math.Round(detection.Y * scaleY),
                detectedWidth, detectedHeight, detection.Score));
        }
        return result;
    }

    private static DenseTensor<float> ToTensor(Mat image, bool normalize, float divisor)
    {
        var tensor = new DenseTensor<float>([1, 3, image.Height, image.Width]);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image.At<Vec3b>(y, x);
            tensor[0, 0, y, x] = normalize ? (pixel.Item2 - 127.5f) / divisor : pixel.Item2;
            tensor[0, 1, y, x] = normalize ? (pixel.Item1 - 127.5f) / divisor : pixel.Item1;
            tensor[0, 2, y, x] = normalize ? (pixel.Item0 - 127.5f) / divisor : pixel.Item0;
        }
        return tensor;
    }

    private static DenseTensor<float> ToRecognitionTensor(Mat image, FaceRecognitionEngineKind engine)
    {
        var tensor = new DenseTensor<float>([1, 3, image.Height, image.Width]);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image.At<Vec3b>(y, x);
            tensor[0, 0, y, x] = FaceRecognitionMath.PrepareInputValue(pixel.Item2, engine);
            tensor[0, 1, y, x] = FaceRecognitionMath.PrepareInputValue(pixel.Item1, engine);
            tensor[0, 2, y, x] = FaceRecognitionMath.PrepareInputValue(pixel.Item0, engine);
        }
        return tensor;
    }

    private static Mat Align(Mat source, IReadOnlyList<Point2f> landmarks)
    {
        var geometry = FaceAlignmentGeometry.Estimate(landmarks.Select(point => new System.Numerics.Vector2(point.X, point.Y)).ToArray());
        var (a, b, offsetX, offsetY) = geometry;
        using var transform = new Mat(2, 3, MatType.CV_64FC1);
        transform.Set(0, 0, a); transform.Set(0, 1, -b); transform.Set(0, 2, offsetX);
        transform.Set(1, 0, b); transform.Set(1, 1, a); transform.Set(1, 2, offsetY);
        var aligned = new Mat();
        Cv2.WarpAffine(source, aligned, transform, new CvSize(112, 112), InterpolationFlags.Linear, BorderTypes.Constant);
        return aligned;
    }

    private static InferenceSession GetSession(string path) =>
        Sessions.GetOrAdd(Path.GetFullPath(path), static model => new(() =>
        {
            var options = new SessionOptions
            {
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                EnableCpuMemArena = true,
                EnableMemoryPattern = true
            };
            options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
            return new InferenceSession(model, options);
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
}

