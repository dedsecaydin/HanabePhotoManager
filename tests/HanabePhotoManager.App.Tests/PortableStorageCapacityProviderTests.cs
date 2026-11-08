using System.IO;
using HanabePhotoManager.App.Platform;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PortableStorageCapacityProviderTests
{
    [Fact]
    public void ExistingDirectoryReportsCapacityAndMissingDirectoryDoesNot()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var provider = new PortableStorageCapacityProvider();
            var capacity = provider.GetCapacity(root);
            Assert.NotNull(capacity);
            Assert.True(capacity.Value.Total > 0);
            Assert.True(capacity.Value.Free <= capacity.Value.Total);
            Assert.Null(provider.GetCapacity(Path.Combine(root, "missing")));
        }
        finally { Directory.Delete(root, true); }
    }
}
