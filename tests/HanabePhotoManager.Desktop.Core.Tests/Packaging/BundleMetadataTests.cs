using System.Xml.Linq;
using FluentAssertions;

namespace HanabePhotoManager.Desktop.Core.Tests.Packaging;

public sealed class BundleMetadataTests
{
    [Fact]
    public void InfoPlist_DeclaresRequiredMacOsBundleMetadata()
    {
        var infoPlistPath = Path.Combine(FindRepositoryRoot(), "src", "HanabePhotoManager.Desktop", "Info.plist");
        var document = XDocument.Load(infoPlistPath);
        var values = document.Root!
            .Element("dict")!
            .Elements()
            .Chunk(2)
            .ToDictionary(pair => pair[0].Value, pair => pair[1]);

        values["CFBundleIdentifier"].Value.Should().Be("com.hanabe.photomanager");
        values["CFBundleExecutable"].Value.Should().Be("HanabePhotoManager.Desktop");
        values["LSMinimumSystemVersion"].Value.Should().Be("11.0");
        values["NSHighResolutionCapable"].Name.LocalName.Should().Be("true");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
