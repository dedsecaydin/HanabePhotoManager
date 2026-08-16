using System.IO;
using HanabePhotoManager.InstallerShell;
using Xunit;

namespace HanabePhotoManager.InstallerShell.Tests;

public sealed class InstallerAppearanceTests
{
    [Fact]
    public void InstallerUsesRoundedIndeterminateProgressTemplate()
    {
        var xaml = File.ReadAllText(ProjectFile("installer", "HanabePhotoManager.InstallerShell", "MainWindow.xaml"));

        Assert.Contains("x:Key=\"RoundedIndeterminateProgressBar\"", xaml);
        Assert.Contains("CornerRadius=\"4\"", xaml);
        Assert.Contains("x:Name=\"IndicatorTransform\"", xaml);
        Assert.Contains("Style=\"{StaticResource RoundedIndeterminateProgressBar}\"", xaml);
    }

    [Fact]
    public void InstallerOffersOptionalDesktopShortcut()
    {
        var xaml = File.ReadAllText(ProjectFile("installer", "HanabePhotoManager.InstallerShell", "MainWindow.xaml"));
        Assert.Contains("x:Name=\"DesktopShortcutCheckBox\"", xaml);

        var arguments = InstallerEngine.BuildInstallArguments("setup.msi", @"D:\Apps\Hanabe", true, "install.log");
        Assert.Contains("CREATE_DESKTOP_SHORTCUT=1", arguments);
        Assert.Contains("INSTALLFOLDER=\"D:\\Apps\\Hanabe\"", arguments);
    }

    private static string ProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory!.FullName, .. parts]);
    }
}
