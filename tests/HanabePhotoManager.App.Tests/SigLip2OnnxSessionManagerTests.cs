using System.Text.Json;
using Xunit;
using System.IO;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.Tests;

public class SigLip2OnnxSessionManagerTests
{
    [Fact]
    public void Initialize_ThrowsWhenManifestMissing()
    {
        var temp = Path.Combine(Path.GetTempPath(), "siglip2-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        var modelsRoot = Path.Combine(temp, "Models", "SigLIP2");
        Directory.CreateDirectory(modelsRoot);

        var ex = Assert.Throws<FileNotFoundException>(() => SigLip2OnnxSessionManager.Initialize(temp));
        Assert.Contains("model_manifest.json", ex.Message);
    }

    [Fact]
    public void Initialize_ThrowsWhenManifestInvalidJson()
    {
        var temp = Path.Combine(Path.GetTempPath(), "siglip2-test-" + Guid.NewGuid().ToString("N"));
        var modelsRoot = Path.Combine(temp, "Models", "SigLIP2");
        Directory.CreateDirectory(modelsRoot);
        File.WriteAllText(Path.Combine(modelsRoot, "model_manifest.json"), "{ invalid json");

        var ex = Assert.Throws<InvalidDataException>(() => SigLip2OnnxSessionManager.Initialize(temp));
        Assert.Contains("model_manifest.json is invalid JSON", ex.Message);
    }

    [Fact]
    public void Initialize_ThrowsWhenModelFileMissing()
    {
        var temp = Path.Combine(Path.GetTempPath(), "siglip2-test-" + Guid.NewGuid().ToString("N"));
        var modelsRoot = Path.Combine(temp, "Models", "SigLIP2");
        Directory.CreateDirectory(modelsRoot);
        var manifest = new {
            model_id = "m",
            model_file = "nonexistent.onnx"
        };
        File.WriteAllText(Path.Combine(modelsRoot, "model_manifest.json"), JsonSerializer.Serialize(manifest));

        var ex = Assert.Throws<FileNotFoundException>(() => SigLip2OnnxSessionManager.Initialize(temp));
        Assert.Contains("SigLIP2 model file not found", ex.Message);
    }

    [Fact]
    public void Initialize_ThrowsWhenModelIsLfsPointer()
    {
        var temp = Path.Combine(Path.GetTempPath(), "siglip2-test-" + Guid.NewGuid().ToString("N"));
        var modelsRoot = Path.Combine(temp, "Models", "SigLIP2");
        Directory.CreateDirectory(modelsRoot);
        var manifest = new {
            model_id = "m",
            model_file = "model.onnx"
        };
        File.WriteAllText(Path.Combine(modelsRoot, "model_manifest.json"), JsonSerializer.Serialize(manifest));
        // write a fake LFS pointer header
        File.WriteAllText(Path.Combine(modelsRoot, "model.onnx"), "version https://git-lfs.github.com/spec/v1\n") ;

        var ex = Assert.Throws<InvalidDataException>(() => SigLip2OnnxSessionManager.Initialize(temp));
        Assert.Contains("Git LFS pointer", ex.Message);
    }

    // Clean up any session if created
    public SigLip2OnnxSessionManagerTests()
    {
        try { SigLip2OnnxSessionManager.DisposeSession(); } catch { }
    }
}
