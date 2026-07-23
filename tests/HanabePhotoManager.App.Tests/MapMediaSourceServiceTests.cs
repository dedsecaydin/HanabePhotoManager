using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MapMediaSourceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-map-source-{Guid.NewGuid():N}");

    [Fact]
    public async Task ScanAsync_AcceptsFilesAndRecursivelyScansFoldersWithoutDuplicates()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "nested")).FullName;
        var first = Create(_root, "one.JPEG");
        var second = Create(nested, "two.webp");
        Create(nested, "ignore.txt");

        var result = await new MapMediaSourceService().ScanAsync([_root, first], true, CancellationToken.None);

        result.Files.Should().BeEquivalentTo([first, second]);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => new MapMediaSourceService().ScanAsync([_root], true, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
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
