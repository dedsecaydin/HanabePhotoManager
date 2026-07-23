using System.IO;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ApplicationIconTests
{
    [Fact]
    public void ProjectAndWindow_UseTheBundledHanabeMultiSizeIcon()
    {
        var root = FindSourceRoot();
        var projectDirectory = Path.Combine(root, "src", "HanabePhotoManager.App");
        var iconPath = Path.Combine(projectDirectory, "Assets", "HanabeApp.ico");

        File.Exists(iconPath).Should().BeTrue("the Hanabe avatar icon must be bundled with the app");

        using var stream = File.OpenRead(iconPath);
        using var reader = new BinaryReader(stream);
        reader.ReadUInt16().Should().Be(0);
        reader.ReadUInt16().Should().Be(1);
        var imageCount = reader.ReadUInt16();
        imageCount.Should().BeGreaterThanOrEqualTo(7, "Windows needs several icon sizes for the EXE, taskbar and title bar");

        var sizes = new HashSet<int>();
        for (var i = 0; i < imageCount; i++)
        {
            var width = reader.ReadByte();
            var height = reader.ReadByte();
            sizes.Add(width == 0 ? 256 : width);
            sizes.Add(height == 0 ? 256 : height);
            reader.BaseStream.Position += 14;
        }

        sizes.Should().Contain(new[] { 16, 32, 48, 256 });

        var project = File.ReadAllText(Path.Combine(projectDirectory, "HanabePhotoManager.App.csproj"));
        project.Should().Contain("<ApplicationIcon>Assets\\HanabeApp.ico</ApplicationIcon>");

        var windowXaml = File.ReadAllText(Path.Combine(projectDirectory, "MainWindow.xaml"));
        windowXaml.Should().Contain("Icon=\"Assets/HanabeApp.ico\"");

        var codeBehind = File.ReadAllText(Path.Combine(projectDirectory, "MainWindow.xaml.cs"));
        codeBehind.Should().Contain("_viewModel.AppIconImage ?? DefaultAppIcon");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate HanabePhotoManager.sln");
    }
}
