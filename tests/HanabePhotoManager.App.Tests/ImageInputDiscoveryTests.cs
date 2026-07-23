using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Compression;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImageInputDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-discovery-{Guid.NewGuid():N}");

    [Fact]
    public void Discover_Recurses_Deduplicates_AndMatchesExtensionsIgnoringCase()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "nested")).FullName;
        var first = Create(_root, "one.JPG");
        var second = Create(nested, "two.png");
        Create(nested, "ignore.txt");

        var result = new ImageInputDiscovery().Discover([_root, first], recursive: true, CancellationToken.None);

        result.Files.Should().BeEquivalentTo([first, second]);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Discover_WhenNotRecursive_IgnoresNestedFiles()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "nested")).FullName;
        var first = Create(_root, "one.jpeg");
        Create(nested, "two.webp");

        new ImageInputDiscovery().Discover([_root], false, CancellationToken.None).Files.Should().Equal(first);
    }

    private static string Create(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, name);
        return Path.GetFullPath(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
