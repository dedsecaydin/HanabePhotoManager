using System.Diagnostics;
using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class LibraryDateSnapshotPerformanceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "HanabeDateSnapshotPerformance",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_IndexesTwoThousandFilesInBoundedBatches()
    {
        var jpegDirectory = Path.Combine(_root, "JPG生图");
        Directory.CreateDirectory(jpegDirectory);
        for (var index = 0; index < 2_000; index++)
        {
            File.Create(Path.Combine(jpegDirectory, $"{index:D4}.jpg")).Dispose();
        }

        var batches = new List<LibraryDateSnapshotBatch>();
        var progress = new InlineProgress<LibraryDateSnapshotBatch>(batches.Add);
        var stopwatch = Stopwatch.StartNew();

        var snapshot = await new LibraryDateSnapshotService()
            .LoadAsync(_root, progress);

        stopwatch.Stop();
        snapshot.Items.Should().HaveCount(2_000);
        batches.Should().HaveCount(32);
        batches.Should().OnlyContain(batch =>
            batch.Items.Count > 0 && batch.Items.Count <= 64);
        batches.Sum(batch => batch.Items.Count).Should().Be(2_000);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
