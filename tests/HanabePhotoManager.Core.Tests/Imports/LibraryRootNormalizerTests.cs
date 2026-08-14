using FluentAssertions;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Core.Tests.Imports;

public sealed class LibraryRootNormalizerTests
{
    [Fact]
    public void Normalize_SingleBackslashRootRelative_PrefersReachableUnc()
    {
        // 根相对路径 "\Hanabe\拍照" 优先识别为丢失反斜杠的 UNC：补双反斜杠成 "\\Hanabe\拍照"
        // 且可访问时返回 UNC，绝不 GetFullPath 成 "C:\Hanabe\拍照"（本机残留副本）。
        var result = LibraryRootNormalizer.Normalize(@"\Hanabe\拍照", _ => true);

        result.Should().Be(@"\\Hanabe\拍照");
        result.Should().NotBe(Path.GetFullPath(@"\Hanabe\拍照"));
        Path.IsPathFullyQualified(result!).Should().BeTrue();
    }

    [Fact]
    public void Normalize_SingleBackslashRootRelative_FallsBackToDriveAbsoluteWhenUncUnreachable()
    {
        // UNC 候选不可访问时才回退 GetFullPath（当前盘符绝对路径），结果仍是完全限定路径。
        var result = LibraryRootNormalizer.Normalize(@"\Hanabe\拍照", _ => false);

        var expected = Path.GetFullPath(@"\Hanabe\拍照");
        Path.IsPathFullyQualified(expected).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Fact]
    public void Normalize_KeepsFullyQualifiedUncUnchanged()
    {
        // 已完全限定的 UNC 路径（\\server\share）原样保留，不经过 GetFullPath、不被改写。
        LibraryRootNormalizer.Normalize(@"\\Hanabe\拍照").Should().Be(@"\\Hanabe\拍照");
        LibraryRootNormalizer.Normalize(@"\\Hanabe\拍照\").Should().Be(@"\\Hanabe\拍照");
    }

    [Fact]
    public void Normalize_KeepsDriveAbsoluteUnchanged()
    {
        LibraryRootNormalizer.Normalize(@"C:\photo").Should().Be(@"C:\photo");
        LibraryRootNormalizer.Normalize(@"C:\photo\").Should().Be(@"C:\photo");
    }

    [Fact]
    public void Normalize_DoesNotCorruptDriveRootPath()
    {
        // TrimEndingDirectorySeparator("C:\") 得 "C:"（不再是完全限定路径），必须保留原样。
        LibraryRootNormalizer.Normalize(@"C:\").Should().Be(@"C:\");
    }

    [Fact]
    public void Normalize_NullOrWhitespaceReturnsAsIs()
    {
        LibraryRootNormalizer.Normalize(null).Should().BeNull();
        LibraryRootNormalizer.Normalize("").Should().Be("");
        LibraryRootNormalizer.Normalize("   ").Should().Be("   ");
    }

    [Fact]
    public void Normalize_InvalidPathReturnsAsIsWithoutThrowing()
    {
        var invalid = new string(Path.GetInvalidPathChars());
        LibraryRootNormalizer.Normalize(invalid).Should().Be(invalid);
    }
}
