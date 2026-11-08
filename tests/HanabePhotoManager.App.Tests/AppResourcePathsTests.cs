using System.IO;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class AppResourcePathsTests
{
    [Fact]
    public void BundleResourcesTakePrecedenceAndStandaloneResourcesStillWork()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "resource-path-" + Guid.NewGuid().ToString("N"));
        var macos = Path.Combine(root, "Contents", "MacOS");
        var resources = Path.Combine(root, "Contents", "Resources");
        Directory.CreateDirectory(Path.Combine(macos, "Models"));
        Directory.CreateDirectory(Path.Combine(resources, "Models"));
        try
        {
            var standalone = Path.Combine(macos, "Models", "model.onnx");
            var bundled = Path.Combine(resources, "Models", "model.onnx");
            File.WriteAllText(standalone, "standalone");
            Assert.Equal(standalone, AppResourcePaths.Find(macos, "Models", "model.onnx"));
            File.WriteAllText(bundled, "bundled");
            Assert.Equal(bundled, AppResourcePaths.Find(macos, "Models", "model.onnx"));
        }
        finally { TemporaryDirectoryCleanup.Delete(root); }
    }
}
