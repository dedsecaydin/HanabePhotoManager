using System.Security.Cryptography;
using FluentAssertions;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Infrastructure.Tests.Files;

public sealed class LibraryContentScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hanabe-scanner-" + Guid.NewGuid().ToString("N"));
    private readonly Sha256FileHasher _hasher = new();
    private readonly LibraryContentScanner _scanner;
    private static readonly HashSet<string> Extensions = new([".jpg", ".mp4"], StringComparer.OrdinalIgnoreCase);

    public LibraryContentScannerTests()
    {
        Directory.CreateDirectory(_root);
        _scanner = new LibraryContentScanner(_hasher);
    }

    [Fact]
    public async Task BuildSizeMapAsync_GroupsFilesBySize()
    {
        var pathA = Path.Combine(_root, "a.jpg");
        var pathB = Path.Combine(_root, "b.jpg");
        var pathC = Path.Combine(_root, "c.jpg");
        await File.WriteAllBytesAsync(pathA, [1, 2, 3]);
        await File.WriteAllBytesAsync(pathB, [1, 2, 3]);
        await File.WriteAllBytesAsync(pathC, [4, 5]);

        var map = await _scanner.BuildSizeMapAsync(_root, Extensions, default);

        map.Should().ContainKey(3L).WhoseValue.Should().HaveCount(2);
        map.Should().ContainKey(2L).WhoseValue.Should().HaveCount(1);
    }

    [Fact]
    public async Task BuildSizeMapAsync_ReturnsEmptyForNonexistentRoot()
    {
        var map = await _scanner.BuildSizeMapAsync(Path.Combine(_root, "nonexistent"), Extensions, default);
        map.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSizeMapAsync_OnlyIncludesMatchingExtensions()
    {
        await File.WriteAllBytesAsync(Path.Combine(_root, "photo.jpg"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(_root, "readme.txt"), [2]);

        var map = await _scanner.BuildSizeMapAsync(_root, Extensions, default);

        map.Values.SelectMany(v => v).Should().ContainSingle();
        map.Values.Single().Single().Should().EndWith("photo.jpg");
    }

    [Fact]
    public async Task FindContentDuplicateAsync_ReturnsPathWhenContentMatches()
    {
        var existingPath = Path.Combine(_root, "existing.jpg");
        var sourcePath = Path.Combine(_root, "source.jpg");
        await File.WriteAllBytesAsync(existingPath, [10, 20, 30]);
        await File.WriteAllBytesAsync(sourcePath, [10, 20, 30]);

        var map = await _scanner.BuildSizeMapAsync(_root, Extensions, default);
        var duplicate = await _scanner.FindContentDuplicateAsync(sourcePath, map, default);

        duplicate.Should().NotBeNull();
        duplicate.Should().Be(existingPath);
    }

    [Fact]
    public async Task FindContentDuplicateAsync_ReturnsNullWhenNoMatch()
    {
        var existingPath = Path.Combine(_root, "existing.jpg");
        var sourcePath = Path.Combine(_root, "source.jpg");
        await File.WriteAllBytesAsync(existingPath, [1, 2, 3]);
        await File.WriteAllBytesAsync(sourcePath, [4, 5, 6]);

        var map = await _scanner.BuildSizeMapAsync(_root, Extensions, default);
        var duplicate = await _scanner.FindContentDuplicateAsync(sourcePath, map, default);

        duplicate.Should().BeNull();
    }

    [Fact]
    public async Task FindContentDuplicateAsync_ReturnsNullWhenSizeNotInMap()
    {
        var existingPath = Path.Combine(_root, "existing.jpg");
        var sourcePath = Path.Combine(_root, "source.jpg");
        await File.WriteAllBytesAsync(existingPath, [1]);
        await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4, 5]);

        var map = await _scanner.BuildSizeMapAsync(_root, Extensions, default);
        var duplicate = await _scanner.FindContentDuplicateAsync(sourcePath, map, default);

        duplicate.Should().BeNull();
    }

    [Fact]
    public async Task FindContentDuplicateAsync_DoesNotMatchSelf()
    {
        var path = Path.Combine(_root, "only.jpg");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        var map = await _scanner.BuildSizeMapAsync(_root, Extensions, default);
        var duplicate = await _scanner.FindContentDuplicateAsync(path, map, default);

        duplicate.Should().BeNull();
    }

    [Fact]
    public async Task FindAllDuplicatesAsync_ReturnsGroupsOfDuplicates()
    {
        var data1 = new byte[] { 1, 2, 3 };
        var data2 = new byte[] { 4, 5, 6 };

        await File.WriteAllBytesAsync(Path.Combine(_root, "a1.jpg"), data1);
        await File.WriteAllBytesAsync(Path.Combine(_root, "a2.jpg"), data1);
        await File.WriteAllBytesAsync(Path.Combine(_root, "a3.jpg"), data1);
        await File.WriteAllBytesAsync(Path.Combine(_root, "b1.jpg"), data2);
        await File.WriteAllBytesAsync(Path.Combine(_root, "b2.jpg"), data2);
        await File.WriteAllBytesAsync(Path.Combine(_root, "c1.jpg"), [7, 8]);

        var groups = await _scanner.FindAllDuplicatesAsync(_root, Extensions, default);

        groups.Should().HaveCount(2);
        groups.First(g => g.Count == 3).Should().HaveCount(3);
        groups.First(g => g.Count == 2).Should().HaveCount(2);
    }

    [Fact]
    public async Task FindAllDuplicatesAsync_ReturnsEmptyWhenNoDuplicates()
    {
        await File.WriteAllBytesAsync(Path.Combine(_root, "a.jpg"), [1, 2]);
        await File.WriteAllBytesAsync(Path.Combine(_root, "b.jpg"), [3, 4]);
        await File.WriteAllBytesAsync(Path.Combine(_root, "c.jpg"), [5, 6]);

        var groups = await _scanner.FindAllDuplicatesAsync(_root, Extensions, default);

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task FindAllDuplicatesAsync_ScansSubdirectories()
    {
        var subDir = Path.Combine(_root, "sub");
        Directory.CreateDirectory(subDir);
        var data = new byte[] { 9, 9, 9 };

        await File.WriteAllBytesAsync(Path.Combine(_root, "root.jpg"), data);
        await File.WriteAllBytesAsync(Path.Combine(subDir, "sub.jpg"), data);

        var groups = await _scanner.FindAllDuplicatesAsync(_root, Extensions, default);

        groups.Should().HaveCount(1);
        groups[0].Should().HaveCount(2);
    }

    [Fact]
    public async Task FindAllDuplicatesAsync_IgnoresFilesWithDifferentSize()
    {
        var data = new byte[] { 1, 2, 3 };
        await File.WriteAllBytesAsync(Path.Combine(_root, "same1.jpg"), data);
        await File.WriteAllBytesAsync(Path.Combine(_root, "same2.jpg"), data);
        await File.WriteAllBytesAsync(Path.Combine(_root, "diff.jpg"), [1, 2, 3, 4]);

        var groups = await _scanner.FindAllDuplicatesAsync(_root, Extensions, default);

        groups.Should().HaveCount(1);
        groups[0].Should().HaveCount(2);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
