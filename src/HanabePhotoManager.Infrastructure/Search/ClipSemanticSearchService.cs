using HanabePhotoManager.Core.Search;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace HanabePhotoManager.Infrastructure.Search;

public sealed class ClipSemanticSearchService : ISemanticSearchService, IDisposable
{
    private static readonly HashSet<string> ImageExtensions = new([".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff"], StringComparer.OrdinalIgnoreCase);
    private readonly ISemanticIndexStore _store;
    private readonly ModelCatalog _catalog;
    private readonly ClipImagePreprocessor _preprocessor;
    private readonly object _statusLock = new();
    private InferenceSession? _imageSession;
    private InferenceSession? _textSession;
    private ClipTokenizer? _tokenizer;
    private SemanticIndexStatus _status = new(0, 0, false, false, "语义搜索模型未就绪。");

    public ClipSemanticSearchService(ISemanticIndexStore store, ModelCatalog catalog, ClipImagePreprocessor? preprocessor = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _preprocessor = preprocessor ?? new ClipImagePreprocessor();
    }

    public SemanticIndexStatus GetIndexStatus()
    {
        lock (_statusLock) return _status;
    }

    public async Task EnsureIndexAsync(string libraryRoot, IProgress<SemanticIndexStatus>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        EnsureModelLoaded();
        var files = Directory.EnumerateFiles(libraryRoot, "*", SearchOption.AllDirectories)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path))).ToArray();
        var stored = (await _store.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(entry => entry.FileKey, StringComparer.OrdinalIgnoreCase);
        SetStatus(new SemanticIndexStatus(files.Length, 0, true, true, "正在建立语义索引…"), progress);
        var indexed = 0;
        var pending = new List<SemanticIndexEntry>(16);
        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(path);
            var fingerprint = $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
            if (!stored.TryGetValue(path, out var entry) || !string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
                pending.Add(new SemanticIndexEntry(path, fingerprint, info.LastWriteTimeUtc, await EncodeImageAsync(path, cancellationToken).ConfigureAwait(false)));
            indexed++;
            if (pending.Count == 16) { await _store.UpsertAsync(pending, cancellationToken).ConfigureAwait(false); pending.Clear(); }
            SetStatus(new SemanticIndexStatus(files.Length, indexed, true, true, $"正在索引 {indexed:N0}/{files.Length:N0}…"), progress);
        }
        if (pending.Count > 0) await _store.UpsertAsync(pending, cancellationToken).ConfigureAwait(false);
        await _store.RemoveMissingAsync(files, cancellationToken).ConfigureAwait(false);
        SetStatus(new SemanticIndexStatus(files.Length, files.Length, false, true, $"已建立 {files.Length:N0} 张照片的语义索引。"), progress);
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        EnsureModelLoaded();
        var queryEmbedding = EncodeText(query);
        var entries = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return entries.Where(entry => entry.Embedding.Count == queryEmbedding.Length && File.Exists(entry.FileKey))
            .Select(entry => new SemanticSearchResult(entry.FileKey, CosineSimilarity(queryEmbedding, entry.Embedding)))
            .OrderByDescending(result => result.Score).ThenBy(result => result.FileKey, StringComparer.OrdinalIgnoreCase).Take(limit).ToArray();
    }

    private void EnsureModelLoaded()
    {
        if (_imageSession is not null && _textSession is not null && _tokenizer is not null) return;
        if (!_catalog.IsReady) { lock (_statusLock) _status = new(0, 0, false, false, _catalog.GetMissingModelMessage()); throw new FileNotFoundException(_catalog.GetMissingModelMessage()); }
        _tokenizer = new ClipTokenizer(_catalog.VocabularyPath);
        _imageSession = new InferenceSession(_catalog.ImageEncoderPath, new SessionOptions());
        _textSession = new InferenceSession(_catalog.TextEncoderPath, new SessionOptions());
    }

    private async Task<float[]> EncodeImageAsync(string path, CancellationToken cancellationToken)
    {
        var input = await _preprocessor.PreprocessAsync(path, cancellationToken).ConfigureAwait(false);
        var tensor = new DenseTensor<float>(input, [1, 3, ClipImagePreprocessor.InputSize, ClipImagePreprocessor.InputSize]);
        using var output = _imageSession!.Run([NamedOnnxValue.CreateFromTensor(_imageSession.InputMetadata.Keys.First(), tensor)]);
        return Normalize(ReadEmbedding(output.First().AsTensor<float>()));
    }

    private float[] EncodeText(string text)
    {
        var tokenization = _tokenizer!.Tokenize(text);
        var tokenTensor = new DenseTensor<long>(tokenization.TokenIds.ToArray(), [1, tokenization.TokenIds.Count]);
        var maskTensor = new DenseTensor<long>(tokenization.AttentionMask.ToArray(), [1, tokenization.AttentionMask.Count]);
        var inputs = new List<NamedOnnxValue>();
        foreach (var input in _textSession!.InputMetadata.Keys)
        {
            var lowerName = input.ToLowerInvariant();
            inputs.Add(NamedOnnxValue.CreateFromTensor(input, lowerName.Contains("mask") ? maskTensor : tokenTensor));
        }
        using var output = _textSession.Run(inputs);
        return Normalize(ReadEmbedding(output.First().AsTensor<float>()));
    }

    private static float[] ReadEmbedding(Tensor<float> tensor) => tensor.ToArray();
    private static float[] Normalize(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(value => value * value));
        return magnitude <= double.Epsilon ? vector : vector.Select(value => value / (float)magnitude).ToArray();
    }
    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right) => left.Zip(right, static (a, b) => a * b).Sum();
    private void SetStatus(SemanticIndexStatus status, IProgress<SemanticIndexStatus>? progress) { lock (_statusLock) _status = status; progress?.Report(status); }
    public void Dispose() { _imageSession?.Dispose(); _textSession?.Dispose(); }
}
