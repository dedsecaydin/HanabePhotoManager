using FluentAssertions;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Infrastructure.Tests.Files;

public sealed class LibraryResequenceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hanabe-reseq-" + Guid.NewGuid().ToString("N"));

    public LibraryResequenceServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ResequenceDirectory_FillsGapAfterDeletion()
    {
        var dir = Path.Combine(_root, "cat");
        Directory.CreateDirectory(dir);
        // Create JK0001..JK0005, then delete JK0002 and JK0004
        foreach (var i in new[] { 1, 2, 3, 4, 5 })
            File.WriteAllText(Path.Combine(dir, $"JK{i:0000}.JPG"), $"content-{i}");
        File.Delete(Path.Combine(dir, "JK0002.JPG"));
        File.Delete(Path.Combine(dir, "JK0004.JPG"));

        LibraryResequenceService.ResequenceDirectory(dir);

        var files = Directory.GetFiles(dir, "JK*.JPG", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f).ToArray();
        files.Should().HaveCount(3);
        Path.GetFileName(files[0]).Should().Be("JK0001.JPG");
        Path.GetFileName(files[1]).Should().Be("JK0002.JPG");
        Path.GetFileName(files[2]).Should().Be("JK0003.JPG");
    }

    [Fact]
    public void ResequenceDirectory_RenumbersFromScratch()
    {
        var dir = Path.Combine(_root, "cat");
        Directory.CreateDirectory(dir);
        // Files start at JK0003, JK0005, JK0007
        File.WriteAllText(Path.Combine(dir, "JK0003.JPG"), "a");
        File.WriteAllText(Path.Combine(dir, "JK0005.JPG"), "b");
        File.WriteAllText(Path.Combine(dir, "JK0007.JPG"), "c");

        LibraryResequenceService.ResequenceDirectory(dir);

        var files = Directory.GetFiles(dir, "JK*.JPG", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f).ToArray();
        Path.GetFileName(files[0]).Should().Be("JK0001.JPG");
        Path.GetFileName(files[1]).Should().Be("JK0002.JPG");
        Path.GetFileName(files[2]).Should().Be("JK0003.JPG");
    }

    [Fact]
    public void ResequenceDirectory_PreservesSidecarExtensions()
    {
        var dir = Path.Combine(_root, "cat");
        Directory.CreateDirectory(dir);
        // JK0001.JPG + JK0001_02.XML (sidecar), JK0003.JPG + JK0003_02.XML
        // Delete JK0001.* → JK0003 becomes JK0001, sidecar JK0003_02 becomes JK0001_02
        File.WriteAllText(Path.Combine(dir, "JK0001.JPG"), "img1");
        File.WriteAllText(Path.Combine(dir, "JK0001_02.XML"), "xml1");
        File.WriteAllText(Path.Combine(dir, "JK0003.JPG"), "img3");
        File.WriteAllText(Path.Combine(dir, "JK0003_02.XML"), "xml3");
        File.Delete(Path.Combine(dir, "JK0001.JPG"));
        File.Delete(Path.Combine(dir, "JK0001_02.XML"));

        LibraryResequenceService.ResequenceDirectory(dir);

        var allFiles = Directory.GetFiles(dir, "JK*.*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).ToArray();
        allFiles.Should().HaveCount(2);
        allFiles.Should().Contain("JK0001.JPG");
        allFiles.Should().Contain("JK0001_02.XML");
    }

    [Fact]
    public void ResequenceDirectory_NoOpWhenAlreadyContiguous()
    {
        var dir = Path.Combine(_root, "cat");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "JK0001.JPG"), "a");
        File.WriteAllText(Path.Combine(dir, "JK0002.JPG"), "b");

        LibraryResequenceService.ResequenceDirectory(dir);

        var files = Directory.GetFiles(dir, "JK*.JPG", SearchOption.TopDirectoryOnly);
        files.Should().HaveCount(2);
        files.Should().Contain(f => Path.GetFileName(f) == "JK0001.JPG");
        files.Should().Contain(f => Path.GetFileName(f) == "JK0002.JPG");
    }

    [Fact]
    public void ResequenceDirectory_EmptyDirectoryIsNoOp()
    {
        var dir = Path.Combine(_root, "empty");
        Directory.CreateDirectory(dir);

        var act = () => LibraryResequenceService.ResequenceDirectory(dir);
        act.Should().NotThrow();
    }

    [Fact]
    public void ResequenceDirectory_HandlesSwappableNames()
    {
        var dir = Path.Combine(_root, "cat");
        Directory.CreateDirectory(dir);
        // JK0001 and JK0002 exist; after deleting JK0001, JK0002 must become JK0001
        // This tests the temp-rename two-phase approach prevents collisions.
        File.WriteAllText(Path.Combine(dir, "JK0001.JPG"), "old1");
        File.WriteAllText(Path.Combine(dir, "JK0002.JPG"), "old2");
        File.Delete(Path.Combine(dir, "JK0001.JPG"));

        LibraryResequenceService.ResequenceDirectory(dir);

        var files = Directory.GetFiles(dir, "JK*.JPG", SearchOption.TopDirectoryOnly);
        files.Should().ContainSingle();
        Path.GetFileName(files[0]).Should().Be("JK0001.JPG");
        File.ReadAllText(files[0]).Should().Be("old2");
    }

    [Fact]
    public void ResequenceLibrary_WalksAllCategoryDirectories()
    {
        // Create a mini library: root/07月/07.14/JPG生图/
        var catDir = Path.Combine(_root, "07月", "07.14", "JPG生图");
        Directory.CreateDirectory(catDir);
        File.WriteAllText(Path.Combine(catDir, "JK0001.JPG"), "a");
        File.WriteAllText(Path.Combine(catDir, "JK0003.JPG"), "b");
        File.Delete(Path.Combine(catDir, "JK0001.JPG"));

        LibraryResequenceService.ResequenceLibrary(_root);

        var files = Directory.GetFiles(catDir, "JK*.JPG", SearchOption.TopDirectoryOnly);
        files.Should().ContainSingle();
        Path.GetFileName(files[0]).Should().Be("JK0001.JPG");
    }

    [Fact]
    public void ResequenceLibrary_NonexistentRootIsNoOp()
    {
        var act = () => LibraryResequenceService.ResequenceLibrary(Path.Combine(_root, "nonexistent"));
        act.Should().NotThrow();
    }

    [Fact]
    public void ResequenceLibrary_DoesNotRenameFilesInRetouchedDirectory()
    {
        var retouchedDir = Path.Combine(_root, "08月", "08.08", "修后");
        Directory.CreateDirectory(retouchedDir);
        var retouched = Path.Combine(retouchedDir, "JK0003.JPG");
        File.WriteAllText(retouched, "edited");

        LibraryResequenceService.ResequenceLibrary(_root);

        File.Exists(retouched).Should().BeTrue();
        File.Exists(Path.Combine(retouchedDir, "JK0001.JPG")).Should().BeFalse();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
