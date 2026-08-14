using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class LibraryDateSnapshotServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "HanabeDateSnapshotTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_AggregatesSupportedFilesFromOneDate()
    {
        var jpegDirectory = Path.Combine(_root, "JPG生图");
        Directory.CreateDirectory(jpegDirectory);
        await File.WriteAllBytesAsync(Path.Combine(jpegDirectory, "a.jpg"), new byte[12]);
        await File.WriteAllBytesAsync(Path.Combine(jpegDirectory, "b.jpg"), new byte[30]);
        await File.WriteAllTextAsync(Path.Combine(jpegDirectory, "ignored.txt"), "not media");

        var snapshot = await new LibraryDateSnapshotService().LoadAsync(_root);

        snapshot.Items.Should().HaveCount(2);
        snapshot.Items.Should().OnlyContain(item => item.Category == "JPG生图");
        snapshot.Categories.Single(item => item.Name == "JPG生图")
            .Should().BeEquivalentTo(new
            {
                FileCount = 2,
                TotalBytes = 42L
            });
        snapshot.IsPartial.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_ReportsBatchesOfAtMostSixtyFourItems()
    {
        var jpegDirectory = Path.Combine(_root, "JPG生图");
        Directory.CreateDirectory(jpegDirectory);
        for (var index = 0; index < 130; index++)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(jpegDirectory, $"{index:D3}.jpg"),
                [1]);
        }

        var batches = new List<LibraryDateSnapshotBatch>();
        var progress = new InlineProgress<LibraryDateSnapshotBatch>(batches.Add);

        var snapshot = await new LibraryDateSnapshotService()
            .LoadAsync(_root, progress);

        snapshot.Items.Should().HaveCount(130);
        batches.Select(batch => batch.Items.Count).Should().Equal(64, 64, 2);
        batches.Should().OnlyContain(batch => !batch.FromCache);
    }

    [Fact]
    public async Task LoadAsync_EnumeratesEveryCategoryExactlyOnce()
    {
        var fileSystem = new FakeLibraryDateFileSystem(_root);
        fileSystem.SetFiles(
            "JPG生图",
            new LibraryDateFileProperties("a.jpg", "a.jpg", ".jpg", 1, DateTime.UtcNow));

        await new LibraryDateSnapshotService(fileSystem).LoadAsync(_root);

        fileSystem.CategoryEnumerationCounts.Should().HaveCount(
            LibraryDateSnapshotService.DefaultCategoryNames.Count);
        fileSystem.CategoryEnumerationCounts.Values.Should().OnlyContain(count => count == 1);
    }

    [Fact]
    public async Task LoadAsync_WhenCancelled_ThrowsAndDoesNotCachePartialResult()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeLibraryDateFileSystem(_root)
        {
            OnItemRead = count =>
            {
                if (count == 2)
                {
                    cancellation.Cancel();
                }
            }
        };
        fileSystem.SetFiles("JPG生图",
            new LibraryDateFileProperties("a.jpg", "a.jpg", ".jpg", 1, DateTime.UtcNow),
            new LibraryDateFileProperties("b.jpg", "b.jpg", ".jpg", 1, DateTime.UtcNow),
            new LibraryDateFileProperties("c.jpg", "c.jpg", ".jpg", 1, DateTime.UtcNow));
        var service = new LibraryDateSnapshotService(fileSystem);

        var act = () => service.LoadAsync(_root, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        service.CachedSnapshotCount.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_ReturnsPartialSnapshotWhenOneFileCannotBeRead()
    {
        var fileSystem = new FakeLibraryDateFileSystem(_root);
        fileSystem.SetResults("JPG生图",
            LibraryDateFileReadResult.Success(
                new LibraryDateFileProperties("a.jpg", "a.jpg", ".jpg", 12, DateTime.UtcNow)),
            LibraryDateFileReadResult.Failure(
                "locked.jpg",
                "access denied"));
        var service = new LibraryDateSnapshotService(fileSystem);

        var snapshot = await service.LoadAsync(_root);

        snapshot.Items.Should().ContainSingle();
        snapshot.IsPartial.Should().BeTrue();
        snapshot.Warnings.Should().ContainSingle(warning =>
            warning.Kind == LibraryDateSnapshotWarningKind.FileUnavailable &&
            warning.Path == "locked.jpg");
        service.CachedSnapshotCount.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_CachesOnlyThreeMostRecentlyUsedDates()
    {
        var fileSystem = new FakeLibraryDateFileSystem(_root);
        var service = new LibraryDateSnapshotService(fileSystem);
        var a = Path.Combine(_root, "a");
        var b = Path.Combine(_root, "b");
        var c = Path.Combine(_root, "c");
        var d = Path.Combine(_root, "d");

        await service.LoadAsync(a);
        await service.LoadAsync(b);
        await service.LoadAsync(c);
        await service.LoadAsync(a); // A becomes most recently used.
        await service.LoadAsync(d); // B becomes least recently used and is evicted.
        await service.LoadAsync(b);

        fileSystem.ScanCounts[a].Should().Be(1);
        fileSystem.ScanCounts[b].Should().Be(2);
        service.CachedSnapshotCount.Should().Be(3);
    }

    [Fact]
    public async Task LoadAsync_RescansWhenDirectoryFingerprintChanges()
    {
        var fileSystem = new FakeLibraryDateFileSystem(_root);
        var service = new LibraryDateSnapshotService(fileSystem);

        await service.LoadAsync(_root);
        await service.LoadAsync(_root);
        fileSystem.AdvanceStamp(_root);
        await service.LoadAsync(_root);

        fileSystem.ScanCounts[_root].Should().Be(2);
    }

    [Fact]
    public async Task CalculateCapacityAsync_PreservesUsableTotalWhenOneEntryFails()
    {
        var fileSystem = new FakeLibraryDateFileSystem(_root);
        fileSystem.SetRecursiveResults(
            LibraryDateFileReadResult.Success(
                new LibraryDateFileProperties("a.bin", "a.bin", ".bin", 10, DateTime.UtcNow)),
            LibraryDateFileReadResult.Failure("locked.bin", "access denied"),
            LibraryDateFileReadResult.Success(
                new LibraryDateFileProperties("b.bin", "b.bin", ".bin", 20, DateTime.UtcNow)));

        var result = await new LibraryDateSnapshotService(fileSystem)
            .CalculateCapacityAsync(_root);

        result.TotalBytes.Should().Be(30);
        result.FilesVisited.Should().Be(2);
        result.IsPartial.Should().BeTrue();
        result.Warnings.Should().ContainSingle(warning =>
            warning.Kind == LibraryDateSnapshotWarningKind.CapacityEntryUnavailable);
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

    private sealed class FakeLibraryDateFileSystem(string root) : ILibraryDateFileSystem
    {
        private readonly Dictionary<string, IReadOnlyList<LibraryDateFileReadResult>> _categoryResults =
            new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<LibraryDateFileReadResult> _recursiveResults = [];
        private readonly Dictionary<string, long> _stamps = new(StringComparer.OrdinalIgnoreCase);
        private int _itemReadCount;

        public string Root { get; } = Path.GetFullPath(root);
        public Action<int>? OnItemRead { get; init; }
        public Dictionary<string, int> ScanCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> CategoryEnumerationCounts { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public bool DirectoryExists(string path) => true;

        public DateTime GetDirectoryLastWriteTimeUtc(string path)
        {
            var normalized = Path.GetFullPath(path);
            return new DateTime(_stamps.GetValueOrDefault(normalized, 1), DateTimeKind.Utc);
        }

        public IEnumerable<LibraryDateFileReadResult> EnumerateTopLevelFiles(string path)
        {
            var dateDirectory = Directory.GetParent(Path.GetFullPath(path))!.FullName;
            var category = Path.GetFileName(path);
            CategoryEnumerationCounts[category] =
                CategoryEnumerationCounts.GetValueOrDefault(category) + 1;
            if (category.Equals(
                    LibraryDateSnapshotService.DefaultCategoryNames[0],
                    StringComparison.OrdinalIgnoreCase))
            {
                ScanCounts[dateDirectory] = ScanCounts.GetValueOrDefault(dateDirectory) + 1;
            }
            foreach (var result in _categoryResults.GetValueOrDefault(category, []))
            {
                OnItemRead?.Invoke(++_itemReadCount);
                yield return result;
            }
        }

        public IEnumerable<LibraryDateFileReadResult> EnumerateFilesRecursively(string path)
        {
            foreach (var result in _recursiveResults)
            {
                OnItemRead?.Invoke(++_itemReadCount);
                yield return result;
            }
        }

        public void SetFiles(string category, params LibraryDateFileProperties[] files) =>
            SetResults(category, files.Select(LibraryDateFileReadResult.Success).ToArray());

        public void SetResults(string category, params LibraryDateFileReadResult[] results) =>
            _categoryResults[category] = results;

        public void SetRecursiveResults(params LibraryDateFileReadResult[] results) =>
            _recursiveResults = results;

        public void AdvanceStamp(string path)
        {
            var normalized = Path.GetFullPath(path);
            _stamps[normalized] = _stamps.GetValueOrDefault(normalized, 1) + 1;
        }
    }
}
