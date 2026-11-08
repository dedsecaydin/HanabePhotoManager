using System.IO;
using HanabePhotoManager.Mac.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PortableImportCoordinatorTests
{
    [Fact]
    public async Task CopiesSandboxSourceAndSkipsDuplicateOnSecondImport()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "portable-import-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source", "1108");
        var library = Path.Combine(root, "library");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(library);
        try
        {
            var original = Path.Combine(source, "sample.jpg");
            var data = new byte[] { 1, 2, 3, 4, 5, 6 };
            await File.WriteAllBytesAsync(original, data);
            var engine = new MacImportEngine();
            var items = await engine.AnalyzeAsync([source], null, CancellationToken.None);
            var decisions = engine.CreateDecisions(library, items);
            var first = await engine.RunAsync(library, decisions, items, false, new HashSet<string>(), null, CancellationToken.None);
            Assert.Equal(1, first.Success);
            Assert.Equal(data, await File.ReadAllBytesAsync(original));
            var imported = Directory.GetFiles(library, "*.jpg", SearchOption.AllDirectories);
            Assert.Single(imported);
            Assert.Equal(data, await File.ReadAllBytesAsync(imported[0]));
            var duplicates = await engine.DetectDuplicatesAsync(library, items, CancellationToken.None);
            var second = await engine.RunAsync(library, engine.CreateDecisions(library, items), items, false, duplicates, null, CancellationToken.None);
            Assert.Equal(0, second.Success);
            Assert.Equal(1, second.Skipped);
            Assert.Single(Directory.GetFiles(library, "*.jpg", SearchOption.AllDirectories));
        }
        finally { TemporaryDirectoryCleanup.Delete(root); }
    }
}
