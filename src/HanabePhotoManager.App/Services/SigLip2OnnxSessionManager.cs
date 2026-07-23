using System.Text.Json;
using Microsoft.ML.OnnxRuntime;

namespace HanabePhotoManager.App.Services;

public static class SigLip2OnnxSessionManager
{
    private static readonly SemaphoreSlim _initGate = new(1, 1);
    private static InferenceSession? _session;
    private static Dictionary<string, NodeMetadata>? _inputMetadata;
    private static Dictionary<string, NodeMetadata>? _outputMetadata;
    private static ModelManifest? _manifest;

    public static bool IsInitialized => _session is not null;

    /// <summary>
    /// Initialize the SigLIP2 session manager.
    /// If baseDirectory is null, AppContext.BaseDirectory is used. This overload is provided for tests.
    /// </summary>
    public static void Initialize(string? baseDirectory = null)
    {
        baseDirectory ??= AppContext.BaseDirectory;
        var modelsDir = Path.Combine(baseDirectory, "Models", "SigLIP2");
        var manifestPath = Path.Combine(modelsDir, "model_manifest.json");

        _initGate.Wait();
        try
        {
            if (_session is not null) return; // already initialized

            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("SigLIP2 model manifest not found.", manifestPath);

            ModelManifest manifest;
            try
            {
                var json = File.ReadAllText(manifestPath);
                manifest = JsonSerializer.Deserialize<ModelManifest>(json) ?? throw new InvalidDataException("model_manifest.json deserialized to null");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("SigLIP2 model_manifest.json is invalid JSON.", ex);
            }

            _manifest = manifest;

            if (string.IsNullOrWhiteSpace(manifest.ModelFile))
                throw new InvalidDataException("model_manifest.json does not specify model_file.");

            var modelPath = Path.GetFullPath(Path.Combine(modelsDir, manifest.ModelFile));
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("SigLIP2 model file not found.", modelPath);

            // Check for Git LFS pointer (text file beginning with LFS pointer header)
            using (var stream = File.OpenRead(modelPath))
            using (var reader = new StreamReader(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                var header = reader.ReadLine();
                if (header is not null && header.StartsWith("version https://git-lfs.github.com/spec/v1", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Model file appears to be a Git LFS pointer. Please ensure the real model binary is available.");
                }
            }

            // Create InferenceSession and enumerate inputs/outputs
            try
            {
                var options = new SessionOptions();
                _session = new InferenceSession(modelPath, options);

                // capture metadata
                _inputMetadata = _session.InputMetadata.ToDictionary(kv => kv.Key, kv => kv.Value);
                _outputMetadata = _session.OutputMetadata.ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            catch (OnnxRuntimeException ex)
            {
                throw new InvalidOperationException("Failed to create ONNX InferenceSession for SigLIP2 model.", ex);
            }
        }
        finally
        {
            _initGate.Release();
        }
    }

    public static InferenceSession GetSession()
    {
        if (_session is null) throw new InvalidOperationException("SigLIP2 session is not initialized. Call Initialize() first.");
        return _session;
    }

    public static IReadOnlyDictionary<string, NodeMetadata> GetInputMetadata()
    {
        if (_inputMetadata is null) throw new InvalidOperationException("SigLIP2 session is not initialized. Call Initialize() first.");
        return _inputMetadata;
    }

    public static IReadOnlyDictionary<string, NodeMetadata> GetOutputMetadata()
    {
        if (_outputMetadata is null) throw new InvalidOperationException("SigLIP2 session is not initialized. Call Initialize() first.");
        return _outputMetadata;
    }

    public static ModelManifest GetManifest()
    {
        if (_manifest is null) throw new InvalidOperationException("SigLIP2 manifest is not loaded. Call Initialize() first.");
        return _manifest;
    }

    public static void DisposeSession()
    {
        _initGate.Wait();
        try
        {
            _session?.Dispose();
            _session = null;
            _inputMetadata = null;
            _outputMetadata = null;
            _manifest = null;
        }
        finally
        {
            _initGate.Release();
        }
    }

    private sealed class ModelManifest
    {
        public string? ModelId { get; set; }
        public string? ModelFile { get; set; }
        public int? ImageSize { get; set; }
        public string? InputName { get; set; }
        public string? OutputName { get; set; }
        public int? EmbeddingDimension { get; set; }
        public string? Preprocessing { get; set; }
        public string? LabelPromptTemplate { get; set; }
        public string? ScoreType { get; set; }
        public string? Sha256 { get; set; }
    }
}
