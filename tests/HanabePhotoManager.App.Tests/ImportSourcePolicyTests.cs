using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportSourcePolicyTests
{
    [Fact]
    public void AddRange_NormalizesDeduplicatesAndRejectsParentChildOverlap()
    {
        var root = Path.Combine(Path.GetTempPath(), "hanabe-source-policy", "photos");
        var sources = new List<ImportSourceSettings>();

        var result = ImportSourcePolicy.AddRange(
            sources,
            [root, root + Path.DirectorySeparatorChar, Path.Combine(root, "child")]);

        result.Added.Should().Be(1);
        result.Rejected.Should().Be(2);
        sources.Should().ContainSingle();
        sources[0].Path.Should().Be(Path.GetFullPath(root));
    }

    [Fact]
    public void EnabledScanPaths_ExcludesDisabledSourcesAndOverlappingChildren()
    {
        var root = Path.Combine(Path.GetTempPath(), "hanabe-source-policy");
        var sources = new[]
        {
            new ImportSourceSettings { Path = root, IsEnabled = true },
            new ImportSourceSettings { Path = Path.Combine(root, "child"), IsEnabled = true },
            new ImportSourceSettings { Path = Path.Combine(root, "disabled"), IsEnabled = false }
        };

        ImportSourcePolicy.EnabledScanPaths(sources).Should().Equal(Path.GetFullPath(root));
    }
}
