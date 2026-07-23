using FluentAssertions;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Core.Tests.Imports;

public sealed class MediaGroupBuilderTests
{
    [Fact]
    public void Build_GroupsSonyVideoWithItsXmlSidecars()
    {
        var primary = CreateSource(@"D:\camera\C0001.MP4", 900, 9);
        var xml2 = CreateSource(@"D:\camera\C0001M02.XML", 20, 2);
        var xml1 = CreateSource(@"D:\camera\c0001m01.xml", 10, 1);

        var groups = CreateBuilder().Build(new[] { xml2, primary, xml1 });

        groups.Should().ContainSingle();
        groups[0].GroupKey.Should().Be("C0001");
        groups[0].Category.Should().Be(MediaCategory.Video);
        groups[0].Primary.Should().BeSameAs(primary);
        groups[0].Sidecars.Should().Equal(xml1, xml2);
    }

    [Fact]
    public void Build_GroupsDjiVideoWithSameStemSidecars()
    {
        var primary = CreateSource(@"D:\camera\DJI_20260606171114_0004_D.MP4", 900, 9);
        var lrf = CreateSource(@"D:\camera\dji_20260606171114_0004_d.lrf", 20, 2);
        var aac = CreateSource(@"D:\camera\DJI_20260606171114_0004_D.AAC", 10, 1);

        var groups = CreateBuilder().Build(new[] { lrf, primary, aac });

        groups.Should().ContainSingle();
        groups[0].GroupKey.Should().Be("DJI_20260606171114_0004_D");
        groups[0].Category.Should().Be(MediaCategory.ActionVideo);
        groups[0].Primary.Should().BeSameAs(primary);
        groups[0].Sidecars.Should().Equal(aac, lrf);
    }

    [Fact]
    public void Build_OnlyGroupsSonySidecarsFromThePrimaryDirectory()
    {
        var primaryA = CreateSource(@"D:\camera-a\C0001.MP4", 900, 1);
        var sidecarA = CreateSource(@"D:\camera-a\C0001M01.XML", 10, 2);
        var primaryB = CreateSource(@"D:\camera-b\c0001.mp4", 800, 3);
        var sidecarB = CreateSource(@"D:\camera-b\c0001m02.xml", 20, 4);

        var groups = CreateBuilder().Build(new[] { sidecarB, primaryA, sidecarA, primaryB });

        groups.Should().HaveCount(2);
        groups[0].Primary.Should().BeSameAs(primaryA);
        groups[0].Sidecars.Should().Equal(sidecarA);
        groups[1].Primary.Should().BeSameAs(primaryB);
        groups[1].Sidecars.Should().Equal(sidecarB);
    }

    [Fact]
    public void Build_OnlyGroupsDjiSidecarsFromThePrimaryDirectory()
    {
        var stem = "DJI_20260606171114_0004_D";
        var primaryA = CreateSource($@"D:\camera-a\{stem}.MP4", 900, 1);
        var sidecarA = CreateSource($@"D:\camera-a\{stem}.LRF", 10, 2);
        var primaryB = CreateSource($@"D:\camera-b\{stem.ToLowerInvariant()}.mp4", 800, 3);
        var sidecarB = CreateSource($@"D:\camera-b\{stem.ToLowerInvariant()}.aac", 20, 4);

        var groups = CreateBuilder().Build(new[] { sidecarB, primaryA, sidecarA, primaryB });

        groups.Should().HaveCount(2);
        groups[0].Primary.Should().BeSameAs(primaryA);
        groups[0].Sidecars.Should().Equal(sidecarA);
        groups[1].Primary.Should().BeSameAs(primaryB);
        groups[1].Sidecars.Should().Equal(sidecarB);
    }

    [Theory]
    [InlineData(@"D:\camera\C0001M01.XML", MediaCategory.Video)]
    [InlineData(@"D:\camera\clip.LRF", MediaCategory.ActionVideo)]
    [InlineData(@"D:\camera\clip.AAC", MediaCategory.ActionVideo)]
    public void Build_ClassifiesOrphanSidecarsByExtensionFallback(string path, MediaCategory expectedCategory)
    {
        var orphan = CreateSource(path, 25, 3);

        var groups = CreateBuilder().Build(new[] { orphan });

        groups.Should().ContainSingle();
        groups[0].Category.Should().Be(expectedCategory);
        groups[0].Primary.Should().BeSameAs(orphan);
        groups[0].Sidecars.Should().BeEmpty();
    }

    [Fact]
    public void Build_CreatesSingleFileGroupsForRawAndJpeg()
    {
        var raw = CreateSource(@"D:\camera\A001.ARW", 100, 1);
        var jpeg = CreateSource(@"D:\camera\A001.JPG", 50, 2);

        var groups = CreateBuilder().Build(new[] { raw, jpeg });

        groups.Select(group => (group.Primary, group.Category)).Should().Equal(
            (raw, MediaCategory.Raw),
            (jpeg, MediaCategory.Jpeg));
        groups.Should().OnlyContain(group => group.Sidecars.Count == 0);
    }

    [Fact]
    public void Build_IsDeterministicAndDoesNotModifyInput()
    {
        var input = new List<SourceMediaFile>
        {
            CreateSource(@"D:\camera\z.JPG", 3, 3),
            CreateSource(@"D:\camera\C0001M02.XML", 2, 2),
            CreateSource(@"D:\camera\a.ARW", 1, 1),
            CreateSource(@"D:\camera\C0001.MP4", 4, 4),
            CreateSource(@"D:\camera\c0001m01.xml", 5, 5),
        };
        var originalOrder = input.ToArray();

        var groups = CreateBuilder().Build(input);

        input.Should().Equal(originalOrder);
        groups.Select(group => group.Primary.FullPath).Should().Equal(
            @"D:\camera\a.ARW",
            @"D:\camera\C0001.MP4",
            @"D:\camera\z.JPG");
        groups[1].Sidecars.Select(file => file.FullPath).Should().Equal(
            @"D:\camera\c0001m01.xml",
            @"D:\camera\C0001M02.XML");
    }

    [Fact]
    public void Build_PreservesSidecarMetadata()
    {
        var primary = CreateSource(@"D:\camera\C0001.MP4", 900, 1);
        var writeTime = new DateTimeOffset(2026, 6, 6, 17, 11, 14, TimeSpan.FromHours(8));
        var sidecar = new SourceMediaFile(@"D:\camera\C0001M01.XML", 12_345, writeTime);

        var group = CreateBuilder().Build(new[] { sidecar, primary }).Single();

        group.Sidecars.Should().ContainSingle().Which.Should().BeSameAs(sidecar);
        group.Sidecars[0].Length.Should().Be(12_345);
        group.Sidecars[0].LastWriteTime.Should().Be(writeTime);
    }

    [Theory]
    [InlineData(@"D:\camera\C0001.MP4", @"D:\camera\C0001.MP4")]
    [InlineData(@"D:\camera\C0001.MP4", @"d:\CAMERA\c0001.mp4")]
    [InlineData(@"D:\camera\C0001M01.XML", @"d:\CAMERA\c0001m01.xml")]
    public void Build_RejectsDuplicateWindowsPaths(string firstPath, string duplicatePath)
    {
        var files = new[]
        {
            CreateSource(firstPath, 1, 1),
            CreateSource(duplicatePath, 2, 2),
        };

        var act = () => CreateBuilder().Build(files);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{duplicatePath}*");
    }

    [Theory]
    [InlineData(@"D:\camera\.\C0001.MP4")]
    [InlineData(@"D:\camera\temporary\..\C0001.MP4")]
    [InlineData(@"D:/camera\C0001.MP4")]
    public void Build_RejectsPathsWithEquivalentNormalizedIdentity(string equivalentPath)
    {
        var files = new[]
        {
            CreateSource(@"D:\camera\C0001.MP4", 1, 1),
            CreateSource(equivalentPath, 2, 2),
        };

        var act = () => CreateBuilder().Build(files);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{equivalentPath}*");
    }

    [Theory]
    [InlineData("C0001.MP4")]
    [InlineData(@"camera\C0001.MP4")]
    [InlineData(@".\camera\C0001.MP4")]
    public void Build_RejectsRelativePaths(string path)
    {
        var act = () => CreateBuilder().Build(new[] { CreateSource(path, 1, 1) });

        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{path}*");
    }

    [Fact]
    public void Build_AllowsFullyQualifiedUncPaths()
    {
        var source = CreateSource(@"\\server\share\camera\photo.ARW", 10, 1);

        var group = CreateBuilder().Build(new[] { source }).Single();

        group.Primary.Should().BeSameAs(source);
        group.Category.Should().Be(MediaCategory.Raw);
    }

    [Fact]
    public void Build_SortsByNormalizedPathIdentity()
    {
        var normalizesToZ = CreateSource(@"D:\a\..\z\photo.ARW", 1, 1);
        var inB = CreateSource(@"D:\b\photo.JPG", 2, 2);

        var groups = CreateBuilder().Build(new[] { normalizesToZ, inB });

        groups.Select(group => group.Primary).Should().Equal(inB, normalizesToZ);
    }

    [Fact]
    public void Build_ConsumesEachSidecarExactlyOnce()
    {
        var primary = CreateSource(@"D:\camera\C0001.MP4", 900, 1);
        var sidecar = CreateSource(@"D:\camera\C0001M01.XML", 10, 2);
        var jpeg = CreateSource(@"D:\camera\photo.JPG", 20, 3);

        var groups = CreateBuilder().Build(new[] { sidecar, jpeg, primary });

        groups.SelectMany(group => group.Sidecars)
            .Count(file => ReferenceEquals(file, sidecar))
            .Should().Be(1);
        groups.Select(group => group.Primary)
            .Should().NotContain(file => ReferenceEquals(file, sidecar));
    }

    [Fact]
    public void Build_RejectsNullFileElement()
    {
        var act = () => CreateBuilder().Build(new SourceMediaFile[] { null! });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*null*");
    }

    [Fact]
    public void Build_RejectsBlankFullPath()
    {
        var act = () => CreateBuilder().Build(new[] { CreateSource("   ", 1, 1) });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*FullPath*");
    }

    [Fact]
    public void Build_ReturnsEmptyReadOnlyResultForEmptyInput()
    {
        var groups = CreateBuilder().Build(Array.Empty<SourceMediaFile>());

        groups.Should().BeEmpty();
        groups.Should().NotBeAssignableTo<List<MediaGroup>>();
    }

    private static MediaGroupBuilder CreateBuilder()
    {
        return new MediaGroupBuilder(new MediaClassifier(new[] { ".ARW", ".CR2", ".CR3" }));
    }

    private static SourceMediaFile CreateSource(string path, long length, int writeSecond)
    {
        return new SourceMediaFile(path, length, DateTimeOffset.UnixEpoch.AddSeconds(writeSecond));
    }
}
