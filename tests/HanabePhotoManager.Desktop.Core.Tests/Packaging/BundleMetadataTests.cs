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
        values["CFBundleDisplayName"].Value.Should().Be("Hanabe Photo Manager");
        values["CFBundleName"].Value.Should().Be("Hanabe Photos");
        values["CFBundleName"].Value.Length.Should().BeLessOrEqualTo(
            15,
            "macOS requires CFBundleName to be no longer than 15 characters");
        values["LSMinimumSystemVersion"].Value.Should().Be("14.0");
        values["NSHighResolutionCapable"].Name.LocalName.Should().Be("true");
    }

    [Fact]
    public void BundleScript_UsesMacOsCompatibleChmodSyntax()
    {
        var scriptPath = Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "macos",
            "create-app-bundle.sh");
        var script = File.ReadAllText(scriptPath);

        script.Should().Contain("chmod +x \"$app_host\"");
        script.Should().NotContain(
            "chmod +x --",
            "the BSD chmod shipped with macOS does not support GNU-style double-dash handling");
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
