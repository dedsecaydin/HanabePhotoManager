using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.Core.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportConflictNameResolverTests
{
    [Fact]
    public void Resolve_UsesOneFreePrefixForVideoAndSidecarWithoutOverwritingTargets()
    {
        var root = Path.Combine(Path.GetTempPath(), "HanabeConflictTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var video = Path.Combine(root, "C0001.MP4");
            var sidecar = Path.Combine(root, "C0001M01.XML");
            File.WriteAllText(video, "original video");
            File.WriteAllText(sidecar, "original sidecar");
            File.WriteAllText(Path.Combine(root, "保留02_C0001.MP4"), "another version");
            var source = new SourceMediaFile(Path.Combine(root, "source.MP4"), 1, DateTimeOffset.Now);
            var sourceSidecar = new SourceMediaFile(Path.Combine(root, "source.XML"), 1, DateTimeOffset.Now);
            var group = new MediaGroup("C0001", MediaCategory.Video, source, [sourceSidecar]);
            var item = new ImportPlanItem(Guid.NewGuid(), group,
                [new PlannedFile(source, video, video + ".hanabe-part", ConflictKind.SameNameDifferentContent),
                 new PlannedFile(sourceSidecar, sidecar, sidecar + ".hanabe-part", ConflictKind.SameNameDifferentContent)],
                ConflictKind.SameNameDifferentContent, ImportItemState.Planned);

            var resolved = ImportConflictNameResolver.Resolve(item);

            resolved.Files.Select(file => Path.GetFileName(file.DestinationPath))
                .Should().Equal("保留03_C0001.MP4", "保留03_C0001M01.XML");
            File.ReadAllText(video).Should().Be("original video");
            File.ReadAllText(sidecar).Should().Be("original sidecar");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
