using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Security.Cryptography;
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
                            throw new FileNotFoundException($"SigLIP2 model manifest not found: {manifestPath}", manifestPath);

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
            {
                var headerBytes = new byte[Math.Min(64, (int)stream.Length)];
                _ = stream.Read(headerBytes);
                var header = System.Text.Encoding.ASCII.GetString(headerBytes);
                if (header.StartsWith("version https://git-lfs.github.com/spec/v1", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Model file appears to be a Git LFS pointer. Please ensure the real model binary is available.");
                }
            }
            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                using var stream = File.OpenRead(modelPath);
                var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                if (!string.Equals(actual, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SigLIP2 model SHA-256 does not match model_manifest.json.");
            }

            // Create InferenceSession and enumerate inputs/outputs
            try
            {
                _session = OnnxRuntimeSessionFactory.Create(modelPath);

                // capture metadata
                _inputMetadata = _session.InputMetadata.ToDictionary(kv => kv.Key, kv => kv.Value);
                _outputMetadata = _session.OutputMetadata.ToDictionary(kv => kv.Key, kv => kv.Value);
                if (!string.IsNullOrWhiteSpace(manifest.InputName) && !_inputMetadata.ContainsKey(manifest.InputName))
                    throw new InvalidDataException($"SigLIP2 input '{manifest.InputName}' was not found in the ONNX graph.");
                if (!string.IsNullOrWhiteSpace(manifest.OutputName) && !_outputMetadata.ContainsKey(manifest.OutputName))
                    throw new InvalidDataException($"SigLIP2 output '{manifest.OutputName}' was not found in the ONNX graph.");
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

    public sealed class ModelManifest
    {
        [System.Text.Json.Serialization.JsonPropertyName("model_id")] public string? ModelId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("model_file")] public string? ModelFile { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("image_size")] public int? ImageSize { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("input_name")] public string? InputName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("output_name")] public string? OutputName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("embedding_dimension")] public int? EmbeddingDimension { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("preprocessing")] public string? Preprocessing { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("label_prompt_template")] public string? LabelPromptTemplate { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("score_type")] public string? ScoreType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("sha256")] public string? Sha256 { get; set; }
    }
}
