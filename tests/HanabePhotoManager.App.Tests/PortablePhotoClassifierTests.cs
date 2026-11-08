using System.IO;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PortablePhotoClassifierTests
{
    [Fact]
    public void MobileClipPreprocessingApproximatelyMatchesWindows()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "face-reference.jpg");
        using var source = OpenCvSharp.Cv2.ImRead(path, OpenCvSharp.ImreadModes.Color);
        var method = typeof(MobileClipPhotoClassifier).GetMethod("Preprocess", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        using var legacy = (OpenCvSharp.Mat)method.Invoke(null, [source])!;
        var portable = PortableModelImageProcessor.Load(path, 256, true);
        double sum = 0;
        for (var y = 0; y < 256; y++)
        for (var x = 0; x < 256; x++)
        {
            var bgr = legacy.At<OpenCvSharp.Vec3f>(y, x);
            sum += Math.Abs(portable[0, 0, y, x] - bgr.Item2) + Math.Abs(portable[0, 1, y, x] - bgr.Item1) + Math.Abs(portable[0, 2, y, x] - bgr.Item0);
        }
        // One 8-bit channel level corresponds to about 0.015 after normalization.
        Assert.True(sum / (256 * 256 * 3) < .015, $"Average preprocessing difference: {sum / (256 * 256 * 3)}");
    }

    [Fact]
    public async Task MobileNetRunsOnnxRuntimeAndMatchesWindowsTopCategory()
    {
        var image = Path.Combine(AppContext.BaseDirectory, "Assets", "face-reference.jpg");
        var root = Path.Combine(AppContext.BaseDirectory, "Models", "Classification");
        var model = Path.Combine(root, "mobilenetv2-7.onnx");
        var labels = Path.Combine(root, "imagenet_classes.txt");
        using var legacy = new OnnxPhotoClassifier(model, labels);
        using var portable = new PortableOnnxPhotoClassifier(model, labels, new PortableRuleBasedPhotoClassifier((_, _) => Task.FromResult(false)));
        var expected = await legacy.ClassifyAsync(image, default);
        var actual = await portable.ClassifyAsync(image, default);
        Assert.Equal("onnx-mobilenetv2", actual.EngineId);
        Assert.True(expected.Labels[0].Label == actual.Labels[0].Label, $"Windows: {string.Join(", ", expected.Labels)}; portable: {string.Join(", ", actual.Labels)}");
    }

    [Fact]
    public async Task MobileClipRunsOnnxRuntimeAndMatchesWindowsTopCategory()
    {
        var image = Path.Combine(AppContext.BaseDirectory, "Assets", "face-reference.jpg");
        var root = Path.Combine(AppContext.BaseDirectory, "Models", "MobileCLIP");
        var model = Path.Combine(root, "mobileclip_s2_visual.onnx");
        var labels = Path.Combine(root, "label_embeddings.json");
        using var legacy = new MobileClipPhotoClassifier(model, labels);
        using var portable = new PortableMobileClipPhotoClassifier(model, labels, new PortableRuleBasedPhotoClassifier((_, _) => Task.FromResult(false)));
        var expected = await legacy.ClassifyAsync(image, default);
        var actual = await portable.ClassifyAsync(image, default);
        Assert.Equal("mobileclip-s2-semantic", actual.EngineId);
        Assert.Equal("mobileclip-s2-semantic", expected.EngineId);
        Assert.True(expected.Labels[0].Label == actual.Labels[0].Label, $"Windows: {string.Join(", ", expected.Labels)}; portable: {string.Join(", ", actual.Labels)}");
    }
}
