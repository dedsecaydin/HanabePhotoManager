using System.Collections.Concurrent;
using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

public static class FaceRecognitionRuntimeOptions
{
    private static FaceRecognitionOptions _current = new();
    public static FaceRecognitionOptions Current
    {
        get => Volatile.Read(ref _current);
        set => Volatile.Write(ref _current, value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static FaceModelIdentity CurrentIdentity
    {
        get
        {
            var options = Current;
            if (options.Engine == FaceRecognitionEngineKind.ArcFaceR100
                && options.EvaluateAvailability().IsAvailable)
                return FaceModelIdentity.CreateArcFace(
                    options.DetectorModelPath!, options.RecognizerModelPath!,
                    options.MatchThreshold <= 0 ? FaceRecognitionDefaults.ArcFaceR100Threshold : options.MatchThreshold);
            return FaceModelIdentity.YuNetSFaceCurrent;
        }
    }
}

public static class FaceRecognitionEngineFactory
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

public sealed class OnnxFaceRecognitionEngine : ILocalFaceEmbeddingService
{
    private static readonly ConcurrentDictionary<string, Lazy<InferenceSession>> Sessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Point2f[] AlignmentTemplate =
    [
        new(38.2946f, 51.6963f), new(73.5318f, 51.5014f), new(56.0252f, 71.7366f),
        new(41.5493f, 92.3655f), new(70.7299f, 92.2041f)
    ];
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
        if (landmarks.Count != 5) throw new InvalidDataException("检测器必须输出五个人脸关键点。");
        double sourceX = 0, sourceY = 0, targetX = 0, targetY = 0;
        for (var index = 0; index < 5; index++)
        {
            sourceX += landmarks[index].X; sourceY += landmarks[index].Y;
            targetX += AlignmentTemplate[index].X; targetY += AlignmentTemplate[index].Y;
        }
        sourceX /= 5; sourceY /= 5; targetX /= 5; targetY /= 5;
        double denominator = 0, a = 0, b = 0;
        for (var index = 0; index < 5; index++)
        {
            var sx = landmarks[index].X - sourceX;
            var sy = landmarks[index].Y - sourceY;
            var tx = AlignmentTemplate[index].X - targetX;
            var ty = AlignmentTemplate[index].Y - targetY;
            denominator += sx * sx + sy * sy;
            a += sx * tx + sy * ty;
            b += sx * ty - sy * tx;
        }
        if (denominator <= 1e-8) throw new InvalidDataException("人脸关键点无法对齐。");
        a /= denominator; b /= denominator;
        using var transform = new Mat(2, 3, MatType.CV_64FC1);
        transform.Set(0, 0, a); transform.Set(0, 1, -b); transform.Set(0, 2, targetX - a * sourceX + b * sourceY);
        transform.Set(1, 0, b); transform.Set(1, 1, a); transform.Set(1, 2, targetY - b * sourceX - a * sourceY);
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

internal sealed record YuNetDetection(float X, float Y, float Width, float Height, float Score, Point2f[] Landmarks);

internal static class YuNetDecoder
{
    public static IReadOnlyList<YuNetDetection> Decode(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
        int inputEdge, float scoreThreshold, float nmsThreshold)
    {
        var map = outputs.ToDictionary(output => output.Name, StringComparer.OrdinalIgnoreCase);
        var candidates = new List<YuNetDetection>();
        foreach (var stride in new[] { 8, 16, 32 })
        {
            if (!map.TryGetValue($"cls_{stride}", out var cls)
                || !map.TryGetValue($"obj_{stride}", out var obj)
                || !map.TryGetValue($"bbox_{stride}", out var bbox)
                || !map.TryGetValue($"kps_{stride}", out var kps)) continue;
            var classifications = cls.AsEnumerable<float>().ToArray();
            var objectness = obj.AsEnumerable<float>().ToArray();
            var boxes = bbox.AsEnumerable<float>().ToArray();
            var keypoints = kps.AsEnumerable<float>().ToArray();
            var columns = inputEdge / stride;
            for (var index = 0; index < classifications.Length; index++)
            {
                var score = MathF.Sqrt(Math.Clamp(classifications[index], 0, 1)
                                       * Math.Clamp(objectness[index], 0, 1));
                if (score < scoreThreshold) continue;
                var row = index / columns;
                var column = index % columns;
                var boxOffset = index * 4;
                var centerX = (column + boxes[boxOffset]) * stride;
                var centerY = (row + boxes[boxOffset + 1]) * stride;
                var width = MathF.Exp(boxes[boxOffset + 2]) * stride;
                var height = MathF.Exp(boxes[boxOffset + 3]) * stride;
                var landmarks = new Point2f[5];
                for (var point = 0; point < 5; point++)
                    landmarks[point] = new(
                        (column + keypoints[index * 10 + point * 2]) * stride,
                        (row + keypoints[index * 10 + point * 2 + 1]) * stride);
                candidates.Add(new(centerX - width / 2, centerY - height / 2, width, height, score, landmarks));
            }
        }
        return NonMaximumSuppression(candidates, nmsThreshold);
    }

    private static IReadOnlyList<YuNetDetection> NonMaximumSuppression(
        IEnumerable<YuNetDetection> candidates, float threshold)
    {
        var ordered = candidates.OrderByDescending(item => item.Score).ToList();
        var kept = new List<YuNetDetection>();
        foreach (var candidate in ordered)
        {
            if (kept.All(existing => IntersectionOverUnion(candidate, existing) <= threshold))
                kept.Add(candidate);
        }
        return kept;
    }

    private static float IntersectionOverUnion(YuNetDetection first, YuNetDetection second)
    {
        var x1 = Math.Max(first.X, second.X);
        var y1 = Math.Max(first.Y, second.Y);
        var x2 = Math.Min(first.X + first.Width, second.X + second.Width);
        var y2 = Math.Min(first.Y + first.Height, second.Y + second.Height);
        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var union = first.Width * first.Height + second.Width * second.Height - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
}
