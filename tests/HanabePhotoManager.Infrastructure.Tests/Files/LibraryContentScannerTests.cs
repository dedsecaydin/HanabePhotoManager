using System.Security.Cryptography;
using FluentAssertions;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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

    [Fact]
    public async Task FindVisualDuplicatesAsync_GroupsReencodedCopies()
    {
        // Two byte-different but visually identical images (same pixels, different
        // containers) should be detected as a near-duplicate group, while a clearly
        // different image should not be grouped with them.
        var imageExtensions = new HashSet<string>([".png", ".bmp"], StringComparer.OrdinalIgnoreCase);
        var copyA = Path.Combine(_root, "photo.png");
        var copyB = Path.Combine(_root, "photo.bmp");
        var different = Path.Combine(_root, "different.png");

        WriteSplitImage(copyA, vertical: true);
        WriteSplitImage(copyB, vertical: true);
        WriteSplitImage(different, vertical: false);

        var groups = await _scanner.FindVisualDuplicatesAsync(_root, imageExtensions, null, default);

        groups.Should().ContainSingle();
        groups[0].Should().HaveCount(2);
        groups[0].Should().Contain(copyA).And.Contain(copyB);
        groups[0].Should().NotContain(different);
    }

    [Fact]
    public async Task FindVisualDuplicatesAsync_ReturnsEmptyWhenNoDuplicates()
    {
        var imageExtensions = new HashSet<string>([".png"], StringComparer.OrdinalIgnoreCase);
        var distinctA = Path.Combine(_root, "a.png");
        var distinctB = Path.Combine(_root, "b.png");

        WriteSplitImage(distinctA, vertical: true);
        WriteSplitImage(distinctB, vertical: false);

        var groups = await _scanner.FindVisualDuplicatesAsync(_root, imageExtensions, null, default);

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task FindVisualDuplicatesAsync_ExcludesProvidedPaths()
    {
        var imageExtensions = new HashSet<string>([".png", ".bmp"], StringComparer.OrdinalIgnoreCase);
        var copyA = Path.Combine(_root, "photo.png");
        var copyB = Path.Combine(_root, "photo.bmp");

        WriteSplitImage(copyA, vertical: true);
        WriteSplitImage(copyB, vertical: true);

        // Excluding copyA should leave too few candidates to form a group.
        var groups = await _scanner.FindVisualDuplicatesAsync(
            _root, imageExtensions, new[] { copyA }, default);

        groups.Should().BeEmpty();
    }

    /// <summary>
    /// Writes a 64x64 image split into two halves. When <paramref name="vertical"/>
    /// is true the left half is white and the right half black; otherwise the top
    /// half is white and the bottom half black. Saving the same content in two
    /// different containers yields byte-different files with identical perceptual
    /// hashes.
    /// </summary>
    private static void WriteSplitImage(string path, bool vertical)
    {
        using var image = new Image<Rgba32>(64, 64);
        image.Mutate(ctx =>
        {
            var white = new Rgba32(255, 255, 255);
            var black = new Rgba32(0, 0, 0);
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var on = vertical ? x < 32 : y < 32;
                image[x, y] = on ? white : black;
            }
        });
        image.Save(path);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
