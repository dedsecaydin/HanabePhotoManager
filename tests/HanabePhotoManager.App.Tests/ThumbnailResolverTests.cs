using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public class ThumbnailResolverTests
{
    [Fact]
    public void CachedThumbnailIsInvalidatedWhenFileChanges()
    {
        var root = System.IO.Path.Combine(@"D:\HanabePhoto\.artifacts", "thumbnail-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var path = System.IO.Path.Combine(root, "sample.jpg");
        System.IO.File.WriteAllBytes(path, [1]);
        try
        {
            var backend = new FakeBackend();
            var resolver = new ThumbnailResolver(backend);
            var first = resolver.Load(path, 100);
            Assert.Same(first, resolver.Load(path, 100));
            Assert.Equal(1, backend.Calls);
            System.IO.File.WriteAllBytes(path, [1, 2]);
            Assert.NotSame(first, resolver.Load(path, 100));
            Assert.Equal(2, backend.Calls);
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }

    private sealed class FakeBackend : IThumbnailBackend
    {
        public int Calls { get; private set; }
        public object? Decode(string path, int width) { Calls++; return new object(); }
        public object? Shell(string path, int width, bool allowExtraction) => null;
    }
}
