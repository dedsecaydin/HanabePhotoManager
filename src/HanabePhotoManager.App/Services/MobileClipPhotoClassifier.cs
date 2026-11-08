using System.IO;
using System.Text.Json;
using HanabePhotoManager.App.Models;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

/// <summary>使用 MobileCLIP 图像编码器和预计算标签向量执行多标签分类。</summary>
public sealed class MobileClipPhotoClassifier : IPhotoClassifier, IDisposable
{
    private readonly string _modelPath;
    private readonly string _embeddingsPath;
    private readonly IPhotoClassifier _fallback;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Net? _network;
    private Dictionary<string, float[]>? _labels;

    public MobileClipPhotoClassifier(string modelPath, string embeddingsPath, IPhotoClassifier? fallback = null)
    { _modelPath = modelPath; _embeddingsPath = embeddingsPath; _fallback = fallback ?? new RuleBasedPhotoClassifier(); }
    public string EngineId => "mobileclip-s2-semantic";
    public string Version => "mobileclip-s2-apple-1";

    public async Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(_modelPath) || !File.Exists(_embeddingsPath))
            return await _fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => ClassifyCore(path, cancellationToken), cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is OpenCVException or IOException or JsonException)
        {
            var fallback = await _fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
            return fallback with { Explanation = $"MobileCLIP 推理失败，已回退轻量识别：{fallback.Explanation}" };
        }
        finally { _gate.Release(); }
    }

    private PhotoClassificationResult ClassifyCore(string path, CancellationToken cancellationToken)
    {
        using var source = Cv2.ImRead(path, ImreadModes.Color);
        if (source.Empty()) throw new InvalidDataException("无法解码照片。");
        using var normalized = Preprocess(source);
        using var blob = CvDnn.BlobFromImage(normalized, 1, new CvSize(256, 256), Scalar.All(0), swapRB: true, crop: false);
        var network = _network ??= CvDnn.ReadNetFromOnnx(_modelPath)
            ?? throw new InvalidDataException("无法加载 MobileCLIP ONNX 模型。");
        network.SetInput(blob);
        using var output = network.Forward();
        cancellationToken.ThrowIfCancellationRequested();
        using var flat = output.Reshape(1, 1);
        var embedding = Enumerable.Range(0, flat.Cols).Select(index => flat.At<float>(0, index)).ToArray();
        _labels ??= JsonSerializer.Deserialize<Dictionary<string, float[]>>(File.ReadAllText(_embeddingsPath)) ?? [];
        var labels = RankLabels(embedding, _labels);
        return new PhotoClassificationResult(labels, EngineId, Version,
            $"MobileCLIP 本地语义匹配：{string.Join("、", labels.Select(item => item.Label))}");
    }

    private static Mat Preprocess(Mat source)
    {
        var scale = 256d / Math.Min(source.Width, source.Height);
        var width = Math.Max(256, (int)Math.Round(source.Width * scale));
        var height = Math.Max(256, (int)Math.Round(source.Height * scale));
        using var resized = new Mat();
        Cv2.Resize(source, resized, new CvSize(width, height), 0, 0, InterpolationFlags.Cubic);
        using var crop = new Mat(resized, new Rect((width - 256) / 2, (height - 256) / 2, 256, 256));
        var result = new Mat();
        crop.ConvertTo(result, MatType.CV_32FC3, 1d / 255d);
        var channels = Cv2.Split(result);
        try
        {
            var means = new[] { .40821073, .4578275, .48145466 };
            var stds = new[] { .27577711, .26130258, .26862954 };
            for (var i = 0; i < 3; i++) { Cv2.Subtract(channels[i], Scalar.All(means[i]), channels[i]); Cv2.Divide(channels[i], Scalar.All(stds[i]), channels[i]); }
            Cv2.Merge(channels, result);
            return result;
        }
        catch { result.Dispose(); throw; }
        finally { foreach (var channel in channels) channel.Dispose(); }
    }

    public static IReadOnlyList<PhotoLabelScore> RankLabels(IReadOnlyList<float> image, IReadOnlyDictionary<string, float[]> labels) =>
        PortableClassificationPostprocessing.RankLabels(image, labels);

    public void Dispose() { _network?.Dispose(); _gate.Dispose(); }
}
