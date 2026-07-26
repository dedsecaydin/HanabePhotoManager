using System.IO;
using System.Text.Json;
using HanabePhotoManager.App.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpSize = SixLabors.ImageSharp.Size;

namespace HanabePhotoManager.App.Services;

public sealed class SigLip2PhotoClassifier : IPhotoClassifier, IDisposable
{
    private readonly string _modelPath;
    private readonly string _embeddingsPath;
    private readonly IPhotoClassifier _fallback;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InferenceSession? _session;
    private Dictionary<string, float[]>? _labels;

    public SigLip2PhotoClassifier(string modelPath, string embeddingsPath, IPhotoClassifier? fallback = null)
    {
        _modelPath = modelPath;
        _embeddingsPath = embeddingsPath;
        _fallback = fallback ?? new RuleBasedPhotoClassifier();
    }

    public string EngineId => "siglip2-base-semantic";
    public string Version => "google-siglip2-base-patch16-224";

    public async Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(_modelPath) || !File.Exists(_embeddingsPath))
            return await _fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ClassifyCore(path, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OnnxRuntimeException or IOException or JsonException or InvalidImageContentException)
        {
            var fallback = await _fallback.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
            return fallback with { Explanation = $"SigLIP2 推理失败，已回退轻量识别：{fallback.Explanation}" };
        }
        finally { _gate.Release(); }
    }

    private PhotoClassificationResult ClassifyCore(string path, CancellationToken cancellationToken)
    {
        using var image = ImageSharpImage.Load<Rgb24>(path);
        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new ImageSharpSize(224, 224), Mode = ResizeMode.Crop, Sampler = KnownResamplers.Bicubic
        }));
        var tensor = new DenseTensor<float>([1, 3, 224, 224]);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < 224; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < 224; x++)
                {
                    tensor[0, 0, y, x] = row[x].R / 127.5f - 1;
                    tensor[0, 1, y, x] = row[x].G / 127.5f - 1;
                    tensor[0, 2, y, x] = row[x].B / 127.5f - 1;
                }
            }
        });
        _session ??= OnnxRuntimeSessionFactory.Create(_modelPath);
        using var results = _session.Run(
            [NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.Single(), tensor)]);
        cancellationToken.ThrowIfCancellationRequested();
        var embedding = results.Single().AsEnumerable<float>().ToArray();
        _labels ??= JsonSerializer.Deserialize<Dictionary<string, float[]>>(
            File.ReadAllText(_embeddingsPath)) ?? [];
        var labels = RankLabels(embedding, _labels, MobileClipRuntimeOptions.MaximumLabels,
            MobileClipRuntimeOptions.SimilarityWindow);
        return new PhotoClassificationResult(labels, EngineId, Version,
            $"SigLIP2 本地语义相似度：{string.Join("、", labels.Select(item => item.Label))}");
    }

    public static IReadOnlyList<PhotoLabelScore> RankLabels(
        IReadOnlyList<float> image, IReadOnlyDictionary<string, float[]> labels,
        int maximumLabels, double similarityWindow)
    {
        var normalizedImage = Normalize(image);
        if (normalizedImage.Length == 0) return [];
        var ranked = labels.Select(pair =>
        {
            var normalizedLabel = Normalize(pair.Value);
            var count = Math.Min(normalizedImage.Length, normalizedLabel.Length);
            var similarityScore = Enumerable.Range(0, count)
                .Sum(index => normalizedImage[index] * normalizedLabel[index]);
            return (pair.Key, SimilarityScore: similarityScore);
        }).OrderByDescending(item => item.SimilarityScore).ToArray();
        if (ranked.Length == 0) return [];
        var cutoff = ranked[0].SimilarityScore - Math.Clamp(similarityWindow, .02, .30);
        return ranked.Where(item => item.SimilarityScore >= cutoff)
            .Take(Math.Clamp(maximumLabels, 1, 5))
            .Select(item => new PhotoLabelScore(item.Key, Math.Round(item.SimilarityScore, 3))).ToArray();
    }

    private static float[] Normalize(IReadOnlyList<float> values)
    {
        var norm = Math.Sqrt(values.Sum(value => value * value));
        return norm <= double.Epsilon ? [] : values.Select(value => (float)(value / norm)).ToArray();
    }

    public void Dispose() { _session?.Dispose(); _gate.Dispose(); }
}
