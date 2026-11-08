using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using HanabePhotoManager.App.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Services;

public static class PortablePhotoClassifierFactory
{
    private const string MobileNetHash = "C1C513582D56AFCEFF8516C73804E484C81C6A830712AB6D682253F4A3CD042F";
    public static IPhotoClassifier Create(string? mode)
    {
        var fallback = new PortableRuleBasedPhotoClassifier();
        if (string.Equals(mode, PhotoClassifierFactory.MobileClipMode, StringComparison.Ordinal))
        {
            var root = AppResourcePaths.Find(AppContext.BaseDirectory, "Models", "MobileCLIP");
            return new PortableMobileClipPhotoClassifier(Path.Combine(root, "mobileclip_s2_visual.onnx"), Path.Combine(root, "label_embeddings.json"), fallback);
        }
        if (!string.Equals(mode, PhotoClassifierFactory.OnnxMode, StringComparison.Ordinal)) return fallback;
        var directory = AppResourcePaths.Find(AppContext.BaseDirectory, "Models", "Classification");
        return new PortableOnnxPhotoClassifier(Path.Combine(directory, "mobilenetv2-7.onnx"), Path.Combine(directory, "imagenet_classes.txt"), fallback, MobileNetHash);
    }
}

internal static class PortableModelImageProcessor
{
    internal static DenseTensor<float> Load(string path, int cropSize, bool mobileClip)
    {
        using var image = Image.Load<Rgb24>(path);
        var scale = 256d / Math.Min(image.Width, image.Height);
        var width = Math.Max(cropSize, (int)Math.Round(image.Width * scale));
        var height = Math.Max(cropSize, (int)Math.Round(image.Height * scale));
        using var cropped = mobileClip
            ? CropCubic(image, width, height, cropSize)
            : image.Clone(operation => operation.Resize(width, height, KnownResamplers.Box)
                .Crop(new Rectangle((width - cropSize) / 2, (height - cropSize) / 2, cropSize, cropSize)));
        float[] means = mobileClip ? [.48145466f, .4578275f, .40821073f] : [.485f, .456f, .406f];
        float[] deviations = mobileClip ? [.26862954f, .26130258f, .27577711f] : [.229f, .224f, .225f];
        var tensor = new DenseTensor<float>([1, 3, cropSize, cropSize]);
        for (var y = 0; y < cropSize; y++)
        for (var x = 0; x < cropSize; x++)
        {
            var pixel = cropped[x, y];
            tensor[0, 0, y, x] = (pixel.R / 255f - means[0]) / deviations[0];
            tensor[0, 1, y, x] = (pixel.G / 255f - means[1]) / deviations[1];
            tensor[0, 2, y, x] = (pixel.B / 255f - means[2]) / deviations[2];
        }
        return tensor;
    }

    // OpenCV INTER_CUBIC uses a=-0.75 and does not widen its kernel when reducing.
    private static Image<Rgb24> CropCubic(Image<Rgb24> source, int resizedWidth, int resizedHeight, int cropSize)
    {
        var result = new Image<Rgb24>(cropSize, cropSize);
        var offsetX = (resizedWidth - cropSize) / 2; var offsetY = (resizedHeight - cropSize) / 2;
        for (var y = 0; y < cropSize; y++)
        for (var x = 0; x < cropSize; x++)
        {
            var sx = (x + offsetX + .5) * source.Width / resizedWidth - .5;
            var sy = (y + offsetY + .5) * source.Height / resizedHeight - .5;
            var ix = (int)Math.Floor(sx); var iy = (int)Math.Floor(sy);
            double red = 0, green = 0, blue = 0;
            for (var dy = -1; dy <= 2; dy++)
            for (var dx = -1; dx <= 2; dx++)
            {
                var weight = Cubic(sx - (ix + dx)) * Cubic(sy - (iy + dy));
                var pixel = source[Math.Clamp(ix + dx, 0, source.Width - 1), Math.Clamp(iy + dy, 0, source.Height - 1)];
                red += pixel.R * weight; green += pixel.G * weight; blue += pixel.B * weight;
            }
            static byte channel(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            result[x, y] = new Rgb24(channel(red), channel(green), channel(blue));
        }
        return result;
    }

    private static double Cubic(double distance)
    {
        const double a = -.75;
        var x = Math.Abs(distance);
        return x <= 1 ? (a + 2) * x * x * x - (a + 3) * x * x + 1 :
            x < 2 ? a * x * x * x - 5 * a * x * x + 8 * a * x - 4 * a : 0;
    }
}

public sealed class PortableOnnxPhotoClassifier(string modelPath, string labelsPath, IPhotoClassifier fallback, string? expectedHash = null) : IPhotoClassifier, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InferenceSession? _session;
    private string[]? _labels;
    public string EngineId => "onnx-mobilenetv2";
    public string Version => "mobilenetv2-7-c1c51358-map1";

    public async Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!AssetsAreUsable())
        {
            var result = await fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
            return result with { Explanation = $"ONNX 模型不可用，已自动改用轻量规则识别。{result.Explanation}" };
        }
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => Run(path, cancellationToken), cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is OnnxRuntimeException or IOException or InvalidDataException or UnknownImageFormatException or InvalidImageContentException)
        {
            var result = await fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
            return result with { Explanation = $"ONNX 推理失败，已自动改用轻量规则识别。{result.Explanation}" };
        }
        finally { _gate.Release(); }
    }

    private PhotoClassificationResult Run(string path, CancellationToken token)
    {
        var session = _session ??= new InferenceSession(modelPath);
        var labels = _labels ??= File.ReadAllLines(labelsPath).Where(label => !string.IsNullOrWhiteSpace(label)).ToArray();
        if (labels.Length < 1000) throw new InvalidDataException("ImageNet 标签文件不完整。");
        var input = PortableModelImageProcessor.Load(path, 224, false);
        using var output = session.Run([NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), input)]);
        token.ThrowIfCancellationRequested();
        var probabilities = PortableClassificationPostprocessing.Softmax(output.First().AsEnumerable<float>().Select(value => (double)value).ToArray());
        var offset = probabilities.Length == labels.Length + 1 ? 1 : 0;
        var ranked = probabilities.Select((score, index) => (Index: index, Score: score))
            .Where(item => item.Index >= offset && item.Index - offset < labels.Length).OrderByDescending(item => item.Score).Take(12)
            .Select(item => (labels[item.Index - offset], item.Score)).ToArray();
        var mapped = PortableClassificationPostprocessing.MapImageNetLabels(ranked);
        if (mapped.Count == 0) mapped = [new PhotoLabelScore("待分类", 1)];
        var top = string.Join("、", ranked.Take(3).Select(item => item.Item1));
        return new PhotoClassificationResult(mapped, EngineId, Version, string.IsNullOrWhiteSpace(top) ? "模型未返回可靠对象" : $"本地模型识别到：{top}");
    }

    private bool AssetsAreUsable()
    {
        if (!File.Exists(modelPath) || !File.Exists(labelsPath)) return false;
        if (string.IsNullOrWhiteSpace(expectedHash)) return true;
        using var stream = File.OpenRead(modelPath);
        return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), expectedHash, StringComparison.OrdinalIgnoreCase);
    }
    public void Dispose() { _session?.Dispose(); _gate.Dispose(); }
}

public sealed class PortableMobileClipPhotoClassifier(string modelPath, string labelsPath, IPhotoClassifier fallback) : IPhotoClassifier, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InferenceSession? _session;
    private Dictionary<string, float[]>? _labels;
    public string EngineId => "mobileclip-s2-semantic";
    public string Version => "mobileclip-s2-apple-1";

    public async Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(modelPath) || !File.Exists(labelsPath)) return await fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => Run(path, cancellationToken), cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is OnnxRuntimeException or IOException or JsonException or UnknownImageFormatException or InvalidImageContentException)
        {
            var result = await fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
            return result with { Explanation = $"MobileCLIP 推理失败，已回退轻量识别：{result.Explanation}" };
        }
        finally { _gate.Release(); }
    }

    private PhotoClassificationResult Run(string path, CancellationToken token)
    {
        var session = _session ??= new InferenceSession(modelPath);
        var input = PortableModelImageProcessor.Load(path, 256, true);
        using var output = session.Run([NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), input)]);
        token.ThrowIfCancellationRequested();
        var embedding = output.First().AsEnumerable<float>().ToArray();
        _labels ??= JsonSerializer.Deserialize<Dictionary<string, float[]>>(File.ReadAllText(labelsPath)) ?? [];
        var labels = PortableClassificationPostprocessing.RankLabels(embedding, _labels);
        return new PhotoClassificationResult(labels, EngineId, Version, $"MobileCLIP 本地语义匹配：{string.Join("、", labels.Select(item => item.Label))}");
    }
    public void Dispose() { _session?.Dispose(); _gate.Dispose(); }
}
