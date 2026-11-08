using System.IO;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.Mac.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacOriginMetadataStoreTests
{
    [Fact]
    public async Task WriteThenRead_VerifiesMetadataWithoutModifyingFileContents()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "mac-origin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "sample.jpg");
            await File.WriteAllTextAsync(path, "original bytes");
            string? attribute = null;
            var store = new MacOriginMetadataStore((args, _) =>
            {
                if (args[0] == "-w") { attribute = args[2]; return Task.FromResult((0, "", "")); }
                return Task.FromResult(attribute is null ? (1, "", "missing") : (0, attribute, ""));
            });
            var metadata = new FileOriginMetadata("source.jpg", 14, new string('A', 64));
            Assert.True((await store.WriteAndVerifyAsync(path, metadata, default)).Success);
            Assert.Equal(metadata, await store.ReadAsync(path, default));
            Assert.Equal("original bytes", await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(root, true); }
    }
}
