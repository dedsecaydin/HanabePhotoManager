using System.Collections.Concurrent;
using System.IO;
using System.Numerics;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Services;

/// <summary>Constructs the shared ONNX face engine without an OpenCV dependency.</summary>
public static class PortableFaceRecognitionFactory
{
    private static readonly ConcurrentDictionary<string, PortableFaceRecognitionEngine> Engines = new(StringComparer.Ordinal);

    public static ILocalFaceEmbeddingService Create(FaceRecognitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var availability = options.EvaluateAvailability();
        if (!availability.IsAvailable) throw new InvalidOperationException(availability.Reason);
        var root = AppResourcePaths.Find(AppContext.BaseDirectory, "Models", "Face");
        var detector = options.Engine == FaceRecognitionEngineKind.YuNetSFace ? Path.Combine(root, "face_detection_yunet_2023mar.onnx") : options.DetectorModelPath!;
        var recognizer = options.Engine == FaceRecognitionEngineKind.YuNetSFace ? Path.Combine(root, "face_recognition_sface_2021dec.onnx") : options.RecognizerModelPath!;
        var identity = options.Engine == FaceRecognitionEngineKind.YuNetSFace ? FaceModelIdentity.YuNetSFaceCurrent :
            FaceModelIdentity.CreateArcFace(detector, recognizer, options.MatchThreshold <= 0 ? FaceRecognitionDefaults.ArcFaceR100Threshold : options.MatchThreshold);
        var limits = FaceRuntimeLimits.For(options.Profile);
        var key = $"{identity.StorageKey}:{options.Profile}:{options.MaxConcurrency}:{options.BatchSize}";
        return Engines.GetOrAdd(key, _ => new PortableFaceRecognitionEngine(detector, recognizer, identity, limits,
            options.MaxConcurrency > 0 ? options.MaxConcurrency : limits.MaxConcurrency,
            options.BatchSize > 0 ? options.BatchSize : limits.BatchSize));
    }
}

internal sealed class PortableFaceRecognitionEngine : ILocalFaceEmbeddingService
{
    private static readonly ConcurrentDictionary<string, Lazy<InferenceSession>> Sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly InferenceSession _detector, _recognizer;
    private readonly FaceRuntimeLimits _limits;
    private readonly int _concurrency, _batchSize;
    public FaceModelIdentity ModelIdentity { get; }

    internal PortableFaceRecognitionEngine(string detector, string recognizer, FaceModelIdentity identity, FaceRuntimeLimits limits, int concurrency, int batchSize)
    {
        if (!File.Exists(detector)) throw new FileNotFoundException("人脸检测器模型缺失。", detector);
        if (!File.Exists(recognizer)) throw new FileNotFoundException("人脸识别模型缺失。", recognizer);
        ModelIdentity = identity; _limits = limits; _concurrency = Math.Max(1, concurrency); _batchSize = Math.Max(1, batchSize);
        _detector = GetSession(detector); _recognizer = GetSession(recognizer);
    }

    public async Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken) =>
        await DetectBatchAsync([path], cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<DetectedFace>> DetectBatchAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var nested = await FaceBatchExecutor.RunAsync(paths, _concurrency, _batchSize,
            (path, token) => Task.Run(() => Detect(path, token), token), cancellationToken).ConfigureAwait(false);
        return nested.SelectMany(faces => faces).ToArray();
    }

    private IReadOnlyList<DetectedFace> Detect(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(path)) return [];
        Image<Rgb24> loaded;
        try { loaded = Image.Load<Rgb24>(path); }
        catch (UnknownImageFormatException) { return []; }
        catch (InvalidImageContentException) { return []; }
        using var source = loaded;
        var inputMetadata = _detector.InputMetadata.First();
        var dimensions = inputMetadata.Value.Dimensions;
        var declaredEdge = dimensions.Length >= 4 ? dimensions[^1] : -1;
        var edge = declaredEdge > 0 ? declaredEdge : _limits.MaximumImageEdge;
        var scaleX = source.Width / (float)edge; var scaleY = source.Height / (float)edge;
        using var resized = source.Clone(operation => operation.Resize(edge, edge, KnownResamplers.Box));
        var input = PortableFacePreprocessor.ToDetectorTensor(resized);
        using var outputs = _detector.Run([NamedOnnxValue.CreateFromTensor(inputMetadata.Key, input)]);
        var detections = YuNetDecoder.Decode(outputs, edge, .6f, .3f);
        var result = new List<DetectedFace>(detections.Count);
        foreach (var detection in detections.Take(32))
        {
            token.ThrowIfCancellationRequested();
            var width = (int)Math.Round(detection.Width * scaleX); var height = (int)Math.Round(detection.Height * scaleY);
            if (!FaceRecognitionMath.IsAcceptableFaceDetection(detection.Score, width, height)) continue;
            var landmarks = detection.Landmarks.Select(point => new Vector2(point.X * scaleX, point.Y * scaleY)).ToArray();
            using var aligned = PortableFacePreprocessor.Align(source, FaceAlignmentGeometry.Estimate(landmarks));
            var tensor = PortableFacePreprocessor.ToRecognitionTensor(aligned, ModelIdentity.Engine);
            using var embeddingOutput = _recognizer.Run([NamedOnnxValue.CreateFromTensor(_recognizer.InputMetadata.Keys.First(), tensor)]);
            var embedding = embeddingOutput.First().AsEnumerable<float>().ToArray();
            FaceRecognitionMath.L2Normalize(embedding);
            result.Add(new(path, embedding, (int)Math.Round(detection.X * scaleX), (int)Math.Round(detection.Y * scaleY), width, height, detection.Score));
        }
        return result;
    }

    private static InferenceSession GetSession(string path) => Sessions.GetOrAdd(Path.GetFullPath(path), static model => new(() =>
    {
        var options = new SessionOptions { ExecutionMode = ExecutionMode.ORT_SEQUENTIAL, GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL, EnableCpuMemArena = true, EnableMemoryPattern = true };
        options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        return new InferenceSession(model, options);
    }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
}
