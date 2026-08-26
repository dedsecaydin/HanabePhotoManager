using System.IO;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportWorkflowUiTests
{
    [Fact]
    public void ImportPrimaryAction_HasStandardSpacingAfterAdvancedOptions()
    {
        var xaml = File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("Content=\"开始分析与导入\" Style=\"{StaticResource Button.Primary}\" Margin=\"0,12,0,0\"");
    }

    [Fact]
    public void ImportTip_UsesStableContainerAndTextOnlyTransition()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));

        xaml.Should().Contain("x:Name=\"ImportTipCard\"").And.Contain("Height=\"72\"");
        xaml.Should().Contain("x:Name=\"ImportTipTextTransform\"");
        code.Should().Contain("AnimateImportTipText");
        code.Should().Contain("HandoffBehavior.SnapshotAndReplace");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
