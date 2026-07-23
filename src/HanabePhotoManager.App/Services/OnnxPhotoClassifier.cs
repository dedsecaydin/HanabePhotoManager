using System.IO;
using System.Security.Cryptography;
using HanabePhotoManager.App.Models;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

public sealed class OnnxPhotoClassifier : IPhotoClassifier, IDisposable
{
    public const string OfficialModelSha256 = "C1C513582D56AFCEFF8516C73804E484C81C6A830712AB6D682253F4A3CD042F";
    private readonly string _modelPath;
    private readonly string _labelsPath;
    private readonly IPhotoClassifier _fallback;
    private readonly string? _expectedHash;
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);
    private Net? _network;
    private string[]? _labels;

    public OnnxPhotoClassifier(
        string modelPath,
        string labelsPath,
        IPhotoClassifier? fallback = null,
        string? expectedHash = null)
    {
        _modelPath = modelPath;
        _labelsPath = labelsPath;
        _fallback = fallback ?? new RuleBasedPhotoClassifier();
        _expectedHash = expectedHash;
    }

    public string EngineId => "onnx-mobilenetv2";

    public string Version => "mobilenetv2-7-c1c51358-map1";

    public async Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!AssetsAreUsable())
        {
            var fallback = await _fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
            return fallback with { Explanation = $"ONNX 模型不可用，已自动改用轻量规则识别。{fallback.Explanation}" };
        }

        await _inferenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => ClassifyCore(path, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OpenCVException or IOException or InvalidDataException)
        {
            var fallback = await _fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
            return fallback with { Explanation = $"ONNX 推理失败，已自动改用轻量规则识别。{fallback.Explanation}" };
        }
        finally
        {
            _inferenceGate.Release();
        }
    }

    private PhotoClassificationResult ClassifyCore(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("找不到要分析的照片。", path);
        var network = _network ??= CvDnn.ReadNetFromOnnx(_modelPath)
            ?? throw new InvalidDataException("无法加载 ONNX 模型。");
        var labels = _labels ??= File.ReadAllLines(_labelsPath).Where(label => !string.IsNullOrWhiteSpace(label)).ToArray();
        if (labels.Length < 1000) throw new InvalidDataException("ImageNet 标签文件不完整。");

        using var image = Cv2.ImRead(path, ImreadModes.Color);
        if (image.Empty()) throw new InvalidDataException("无法解码照片。");
        using var normalized = PreprocessImage(image);
        using var blob = CvDnn.BlobFromImage(normalized, 1, new CvSize(224, 224), Scalar.All(0), swapRB: true, crop: false);
        network.SetInput(blob);
        using var output = network.Forward();
        cancellationToken.ThrowIfCancellationRequested();

        using var flat = output.Reshape(1, 1);
        var raw = new double[flat.Cols];
        for (var index = 0; index < raw.Length; index++) raw[index] = flat.At<float>(0, index);
        var probabilities = Softmax(raw);
        var labelOffset = probabilities.Length == labels.Length + 1 ? 1 : 0;
        var ranked = probabilities
            .Select((score, index) => (Index: index, Score: score))
            .Where(item => item.Index >= labelOffset && item.Index - labelOffset < labels.Length)
            .OrderByDescending(item => item.Score)
            .Take(12)
            .Select(item => (labels[item.Index - labelOffset], item.Score))
            .ToArray();
        var mapped = MapImageNetLabels(ranked);
        if (mapped.Count == 0) mapped = [new PhotoLabelScore("待分类", 1)];
        var topObjects = string.Join("、", ranked.Take(3).Select(item => item.Item1));
        return new PhotoClassificationResult(
            mapped, EngineId, Version,
            string.IsNullOrWhiteSpace(topObjects) ? "模型未返回可靠对象" : $"本地模型识别到：{topObjects}");
    }

    public static Mat PreprocessImage(Mat source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Empty()) throw new ArgumentException("图像不能为空。", nameof(source));

        var scale = 256d / Math.Min(source.Width, source.Height);
        var width = Math.Max(224, (int)Math.Round(source.Width * scale));
        var height = Math.Max(224, (int)Math.Round(source.Height * scale));
        using var resized = new Mat();
        Cv2.Resize(source, resized, new CvSize(width, height), 0, 0, InterpolationFlags.Area);
        var x = (width - 224) / 2;
        var y = (height - 224) / 2;
        using var crop = new Mat(resized, new Rect(x, y, 224, 224));
        var normalized = new Mat();
        crop.ConvertTo(normalized, MatType.CV_32FC3, 1d / 255d);
        var channels = Cv2.Split(normalized);
        try
        {
            // Input is BGR; BlobFromImage swaps it to RGB after per-channel normalization.
            Cv2.Subtract(channels[0], new Scalar(0.406), channels[0]);
            Cv2.Divide(channels[0], new Scalar(0.225), channels[0]);
            Cv2.Subtract(channels[1], new Scalar(0.456), channels[1]);
            Cv2.Divide(channels[1], new Scalar(0.224), channels[1]);
            Cv2.Subtract(channels[2], new Scalar(0.485), channels[2]);
            Cv2.Divide(channels[2], new Scalar(0.229), channels[2]);
            Cv2.Merge(channels, normalized);
            return normalized;
        }
        catch
        {
            normalized.Dispose();
            throw;
        }
        finally
        {
            foreach (var channel in channels) channel.Dispose();
        }
    }

    public static IReadOnlyList<PhotoLabelScore> MapImageNetLabels(
        IEnumerable<(string Label, double Score)> predictions)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (label, score) in predictions)
        {
            AddMapped(label, score, scores, "动物",
                "dog", "cat", "terrier", "retriever", "spaniel", "hound", "shepherd", "bird", "eagle", "owl", "fish", "shark", "whale", "monkey", "panda", "bear", "horse", "cow", "ox", "sheep", "goat", "rabbit", "hamster", "turtle", "lizard", "snake", "insect", "butterfly");
            AddMapped(label, score, scores, "美食",
                "food", "dish", "pizza", "burger", "cheeseburger", "hotdog", "bread", "cake", "ice cream", "fruit", "orange", "lemon", "apple", "banana", "strawberry", "pineapple", "wine", "espresso", "soup", "salad");
            AddMapped(label, score, scores, "交通",
                "car", "cab", "taxi", "truck", "bus", "train", "airliner", "aircraft", "bicycle", "motor scooter", "motorcycle", "boat", "ship", "van", "tractor");
            AddMapped(label, score, scores, "建筑",
                "church", "castle", "palace", "monastery", "mosque", "lighthouse", "library", "cinema", "barn", "boathouse", "yurt", "dome");
            AddMapped(label, score, scores, "自然风景",
                "cliff", "valley", "volcano", "lakeside", "seashore", "coral reef", "geyser", "promontory", "sandbar");
            AddMapped(label, score, scores, "植物",
                "flower", "daisy", "rose", "sunflower", "orchid", "corn", "acorn", "mushroom", "buckeye", "rapeseed");
            AddMapped(label, score, scores, "室内",
                "sofa", "chair", "table", "bed", "wardrobe", "bookcase", "refrigerator", "television", "desktop computer", "lamp");
            AddMapped(label, score, scores, "人像",
                "bride", "groom", "ballplayer", "scuba diver");
        }

        return scores.Select(pair => new PhotoLabelScore(pair.Key, Math.Round(Math.Clamp(pair.Value, 0, 1), 3)))
            .OrderByDescending(label => label.Score)
            .ThenBy(label => label.Label, StringComparer.CurrentCulture)
            .ToArray();
    }

    private static void AddMapped(
        string sourceLabel,
        double score,
        IDictionary<string, double> destination,
        string category,
        params string[] terms)
    {
        if (!terms.Any(term => sourceLabel.Contains(term, StringComparison.OrdinalIgnoreCase))) return;
        destination.TryGetValue(category, out var current);
        destination[category] = Math.Min(1, current + score);
    }

    private static double[] Softmax(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return [];
        var maximum = values.Max();
        var exponentials = values.Select(value => Math.Exp(value - maximum)).ToArray();
        var sum = exponentials.Sum();
        return sum <= double.Epsilon ? new double[values.Count] : exponentials.Select(value => value / sum).ToArray();
    }

    private bool AssetsAreUsable()
    {
        if (!File.Exists(_modelPath) || !File.Exists(_labelsPath)) return false;
        if (string.IsNullOrWhiteSpace(_expectedHash)) return true;
        using var stream = File.OpenRead(_modelPath);
        return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), _expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _network?.Dispose();
        _inferenceGate.Dispose();
    }
}
