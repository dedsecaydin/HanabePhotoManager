using System.IO;
using HanabePhotoManager.Mac.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacTrashServiceTests
{
    [Fact]
    public void DelegatesExistingFileToTrashWithoutDeletingItDirectly()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "mac-trash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "sample.jpg");
            File.WriteAllText(path, "test");
            string? sent = null;
            var service = new MacTrashService(value => sent = value);
            service.MoveToRecycleBin(path);
            Assert.Equal(Path.GetFullPath(path), sent);
            Assert.Equal("test", File.ReadAllText(path));
            Assert.Throws<FileNotFoundException>(() => service.MoveToRecycleBin(Path.Combine(root, "missing.jpg")));
        }
        finally { Directory.Delete(root, true); }
    }
}
